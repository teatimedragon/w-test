import { useState } from 'react'
import { Link } from 'react-router-dom'
import { CreateEventWizard } from '../admin/CreateEventWizard'
import { ManageTemplates } from '../admin/ManageTemplates'
import { ViewEvents } from '../admin/ViewEvents'
import type { GameEvent } from '../lib/api'
import { useStore } from '../lib/useStore'

const VIEWS = { create: 'Create Event', view: 'View Events', templates: 'Manage Templates' } as const
type View = keyof typeof VIEWS

export function AdminPage() {
  const store = useStore()
  const [view, setView] = useState<View>('create')
  const [created, setCreated] = useState<GameEvent | null>(null)
  // Remounting the wizard after a create starts it fresh next time.
  const [wizardKey, setWizardKey] = useState(0)

  function onCreated(event: GameEvent) {
    setCreated(event)
    setWizardKey((k) => k + 1)
    setView('view')
  }

  return (
    <main className="page admin">
      <header className="site-header">
        <Link to="/" className="store-name">
          {store?.storeName ?? 'Basic Game Store'}
        </Link>
        <p className="subtitle">Store events admin</p>
      </header>

      <div className="toggle" role="tablist">
        {(Object.keys(VIEWS) as View[]).map((key) => (
          <button
            key={key}
            type="button"
            role="tab"
            aria-selected={view === key}
            className={view === key ? 'active' : ''}
            onClick={() => setView(key)}
          >
            {VIEWS[key]}
          </button>
        ))}
      </div>

      {view === 'create' && <CreateEventWizard key={wizardKey} onCreated={onCreated} />}
      {view === 'view' && (
        <>
          {created && <p className="notice" role="status">Created “{created.name}”.</p>}
          <ViewEvents focusEvent={created} />
        </>
      )}
      {view === 'templates' && <ManageTemplates />}
    </main>
  )
}

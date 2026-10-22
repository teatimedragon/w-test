import { useEffect, useState } from 'react'
import { api, describeError, type Template } from '../lib/api'
import { storeId } from '../lib/config'
import { formatDuration } from '../lib/format'
import { TemplateFields } from './TemplateFields'
import { templateProblems } from './templateProblems'

type Mode = 'select' | 'update' | 'create-values' | 'create-name'

export function ManageTemplates() {
  const [templates, setTemplates] = useState<Template[] | null>(null)
  const [selectedId, setSelectedId] = useState<number | undefined>(0)
  const [mode, setMode] = useState<Mode>('select')
  const [draft, setDraft] = useState<Template | null>(null)
  const [problems, setProblems] = useState<string[]>([])
  const [notice, setNotice] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  async function load(select?: number) {
    try {
      const list = await api.listTemplates(storeId())
      setTemplates(list)
      // Template 0 (Default) is preselected.
      setSelectedId(select ?? list.find((t) => t.templateId === 0)?.templateId ?? list[0]?.templateId)
    } catch (e) {
      setProblems([describeError(e)])
    }
  }

  useEffect(() => {
    load()
  }, [])

  const selected = templates?.find((t) => t.templateId === selectedId)

  function start(next: Mode) {
    if (!selected) return
    setNotice(null)
    setProblems([])
    // Create New copies the selected template's values; the new one gets its own id and name on save.
    setDraft(next === 'update' ? { ...selected } : { ...selected, templateId: undefined, name: '' })
    setMode(next)
  }

  function cancel() {
    setMode('select')
    setDraft(null)
    setProblems([])
  }

  function continueToName() {
    if (!draft) return
    const found = templateProblems(draft, false)
    setProblems(found)
    if (found.length === 0) setMode('create-name')
  }

  async function save() {
    if (!draft) return
    const found = templateProblems(draft)
    setProblems(found)
    if (found.length > 0) return
    setSaving(true)
    try {
      const saved = await api.saveTemplate(storeId(), { ...draft, name: draft.name.trim(), gameType: draft.gameType.trim() })
      await load(saved.templateId)
      setNotice(mode === 'update' ? `Saved "${saved.name}".` : `Created template "${saved.name}".`)
      setMode('select')
      setDraft(null)
    } catch (e) {
      setProblems([describeError(e)])
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="templates">
      {notice && <p className="notice" role="status">{notice}</p>}

      {mode === 'select' && (
        <>
          <fieldset className="template-picker">
            <legend>Templates</legend>
            {templates === null && <p className="muted">Loading templates…</p>}
            {templates?.map((t) => (
              <label key={t.templateId} className="radio">
                <input type="radio" name="manage-template" checked={selectedId === t.templateId} onChange={() => setSelectedId(t.templateId)} />
                <span>
                  <strong>{t.name}</strong>{' '}
                  <span className="muted">
                    #{t.templateId} · {t.gameType} · {t.defaultCapacity} seats, min {t.defaultEventMinimum} · {formatDuration(t.defaultDuration)}
                  </span>
                </span>
              </label>
            ))}
          </fieldset>
          <div className="actions">
            <button type="button" onClick={() => start('update')} disabled={!selected}>
              Update Template
            </button>
            <button type="button" onClick={() => start('create-values')} disabled={!selected}>
              Create New Template
            </button>
          </div>
        </>
      )}

      {mode === 'update' && draft && (
        <>
          <h2>Update “{selected?.name}”</h2>
          <TemplateFields value={draft} onChange={setDraft} />
        </>
      )}

      {mode === 'create-values' && draft && (
        <>
          <h2>New template (copied from “{selected?.name}”)</h2>
          <TemplateFields value={draft} onChange={setDraft} includeName={false} />
        </>
      )}

      {mode === 'create-name' && draft && (
        <>
          <h2>Name the new template</h2>
          <div className="fields">
            <label>
              Template name
              <input value={draft.name} maxLength={100} autoFocus onChange={(e) => setDraft({ ...draft, name: e.target.value })} />
            </label>
          </div>
        </>
      )}

      {problems.length > 0 && (
        <ul className="error" role="alert">
          {problems.map((p) => (
            <li key={p}>{p}</li>
          ))}
        </ul>
      )}

      {mode !== 'select' && (
        <div className="actions">
          <button type="button" className="secondary" onClick={mode === 'create-name' ? () => setMode('create-values') : cancel} disabled={saving}>
            {mode === 'create-name' ? 'Back' : 'Cancel'}
          </button>
          {mode === 'create-values' ? (
            <button type="button" onClick={continueToName}>
              Continue
            </button>
          ) : (
            <button type="button" onClick={save} disabled={saving}>
              {saving ? 'Saving…' : 'Save'}
            </button>
          )}
        </div>
      )}
    </section>
  )
}

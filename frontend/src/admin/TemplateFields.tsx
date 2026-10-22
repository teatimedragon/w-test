import type { Template } from '../lib/api'

/** Editable template values. `name` is left out when the caller asks for it in a separate step. */
export function TemplateFields({
  value,
  onChange,
  includeName = true,
}: {
  value: Template
  onChange: (next: Template) => void
  includeName?: boolean
}) {
  const set = <K extends keyof Template>(key: K, v: Template[K]) => onChange({ ...value, [key]: v })
  const num = (v: string) => (v === '' ? 0 : Number(v))
  return (
    <div className="fields">
      {includeName && (
        <label>
          Template name
          <input value={value.name} maxLength={100} onChange={(e) => set('name', e.target.value)} />
        </label>
      )}
      <label>
        Game type
        <input value={value.gameType} maxLength={100} onChange={(e) => set('gameType', e.target.value)} />
      </label>
      <label>
        Default capacity
        <input type="number" min={1} value={value.defaultCapacity} onChange={(e) => set('defaultCapacity', num(e.target.value))} />
      </label>
      <label>
        Default event minimum
        <input type="number" min={1} value={value.defaultEventMinimum} onChange={(e) => set('defaultEventMinimum', num(e.target.value))} />
      </label>
      <label>
        Default duration (minutes)
        <input type="number" min={1} step={15} value={value.defaultDuration} onChange={(e) => set('defaultDuration', num(e.target.value))} />
      </label>
      <label>
        Default play formats
        <input value={value.defaultPlayFormats} maxLength={250} onChange={(e) => set('defaultPlayFormats', e.target.value)} />
      </label>
      <label>
        Default details
        <textarea value={value.defaultDetails} maxLength={250} onChange={(e) => set('defaultDetails', e.target.value)} />
      </label>
    </div>
  )
}

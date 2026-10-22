import { useEffect, useState } from 'react'
import { api, type Store } from './api'
import { storeId } from './config'

/** The default store's details, or null while loading or if it failed to load. */
export function useStore(): Store | null {
  const [store, setStore] = useState<Store | null>(null)
  useEffect(() => {
    let cancelled = false
    api.getStore(storeId()).then((s) => !cancelled && setStore(s)).catch(() => {})
    return () => {
      cancelled = true
    }
  }, [])
  return store
}

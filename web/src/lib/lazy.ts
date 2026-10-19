import { HUB_BUILD } from './api'

const RELOAD_KEY = 'hub.lazy-reload'
let attemptedBuild: string | undefined

function reloadWasPersisted() {
  try { return sessionStorage.getItem(RELOAD_KEY) === HUB_BUILD } catch { return false }
}

function persistReloadAttempt() {
  try {
    sessionStorage.setItem(RELOAD_KEY, HUB_BUILD)
    return sessionStorage.getItem(RELOAD_KEY) === HUB_BUILD
  } catch {
    return false
  }
}

/** Retry a stale chunk once for this build; storage is the cross-page guard. */
export async function lazyImport<T>(load: () => Promise<T>): Promise<T> {
  try {
    return await load()
  } catch (error) {
    if (attemptedBuild === HUB_BUILD || reloadWasPersisted()) throw error
    if (!persistReloadAttempt()) throw error
    attemptedBuild = HUB_BUILD
    window.location.reload()
    throw error
  }
}

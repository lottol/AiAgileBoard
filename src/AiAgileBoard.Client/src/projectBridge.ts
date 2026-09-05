export type ProjectSettings = Record<string, unknown>

export type ProjectState = {
  type: 'projectState'
  projectName: string | null
  saveStatus: 'saving' | 'saved' | 'failed' | null
  settings: ProjectSettings | null
  error: string | null
  recoveryAvailable: boolean
}

export type ProjectCommand =
  | { command: 'getState' | 'newProject' | 'openProject' | 'closeProject' | 'retrySave' | 'recoverProject' }
  | { command: 'updateSettings'; settings: ProjectSettings }

declare global {
  interface Window {
    __aiabDesktop?: boolean
    chrome?: {
      webview?: {
        postMessage: (message: ProjectCommand) => void
        addEventListener: (type: 'message', listener: (event: MessageEvent<ProjectState>) => void) => void
        removeEventListener: (type: 'message', listener: (event: MessageEvent<ProjectState>) => void) => void
      }
    }
  }
}

export function sendProjectCommand(command: ProjectCommand) {
  window.chrome?.webview?.postMessage(command)
}

/** Persist preferences in the project archive, never browser-local storage. */
export function updateProjectSettings(settings: ProjectSettings) {
  sendProjectCommand({ command: 'updateSettings', settings })
}

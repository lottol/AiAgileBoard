import { useEffect, useState, type ReactNode } from 'react'
import { sendProjectCommand, type ProjectCommand, type ProjectState } from '../projectBridge'

export function ProjectWorkspace({ children }: { children: ReactNode }) {
  const [state, setState] = useState<ProjectState | null>(null)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    const bridge = window.chrome?.webview
    function receive(event: MessageEvent<ProjectState>) {
      if (event.data?.type !== 'projectState') return
      setState(event.data)
      setBusy(false)
    }
    bridge?.addEventListener('message', receive)
    sendProjectCommand({ command: 'getState' })
    return () => bridge?.removeEventListener('message', receive)
  }, [])

  function command(value: ProjectCommand) {
    setBusy(true)
    sendProjectCommand(value)
  }

  if (!state) return <main className="project-home" role="status">Loading project workspace…</main>

  if (!state.projectName) {
    return (
      <main className="project-home">
        <span className="brand-mark" aria-hidden="true"><span /></span>
        <h1>AI Agile Board</h1>
        <p>Open a project or create a new board. Your data and settings stay together in one .aiab file.</p>
        {state.recoveryAvailable && (
          <section className="project-recovery" aria-label="Project recovery">
            <h2>Recover your previous project</h2>
            <p>A working copy was retained after the application stopped. Recover it before opening another project.</p>
            <button className="primary-button" disabled={busy} onClick={() => command({ command: 'recoverProject' })}>Recover Project</button>
          </section>
        )}
        <div className="project-actions">
          <button className="primary-button" disabled={busy || state.recoveryAvailable} onClick={() => command({ command: 'newProject' })}>New Project</button>
          <button className="secondary-button" disabled={busy || state.recoveryAvailable} onClick={() => command({ command: 'openProject' })}>Open Project</button>
        </div>
        {busy && <p role="status">Opening project…</p>}
        {state.error && <p className="form-error" role="alert">{state.error}</p>}
      </main>
    )
  }

  const failed = state.saveStatus === 'failed'
  return (
    <>
      <section className="project-toolbar" aria-label="Project">
        <strong>{state.projectName}</strong>
        <span role="status">{failed ? 'Save failed — changes retained for recovery' : state.saveStatus === 'saving' ? 'Saving…' : 'All changes saved'}</span>
        {failed && <button className="primary-button" disabled={busy} onClick={() => command({ command: 'retrySave' })}>Retry Save</button>}
        <button className="secondary-button" disabled={busy || failed} onClick={() => command({ command: 'closeProject' })}>Close Project</button>
        {state.error && <p className="form-error" role="alert">{state.error}</p>}
      </section>
      <div inert={failed || busy}>{children}</div>
    </>
  )
}

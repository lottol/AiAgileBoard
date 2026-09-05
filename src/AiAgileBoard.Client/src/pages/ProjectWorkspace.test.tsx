import { act, cleanup, fireEvent, render, screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { App } from '../App'
import { type ProjectState, updateProjectSettings } from '../projectBridge'

let receive: (event: MessageEvent<ProjectState>) => void
const postMessage = vi.fn()
const home: ProjectState = {
  type: 'projectState', projectName: null, saveStatus: null,
  settings: null, error: null, recoveryAvailable: false,
}

function state(value: Partial<ProjectState> = {}) {
  act(() => receive(new MessageEvent('message', { data: { ...home, ...value } })))
}

describe('desktop projects', () => {
  beforeEach(() => {
    postMessage.mockReset()
    window.__aiabDesktop = true
    window.chrome = { webview: {
      postMessage,
      addEventListener: (_type, listener) => { receive = listener },
      removeEventListener: vi.fn(),
    } }
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => [] }))
  })
  afterEach(() => {
    cleanup()
    delete window.__aiabDesktop
    delete window.chrome
    vi.unstubAllGlobals()
  })

  it('starts at home without querying tickets and permits cancellation', () => {
    render(<App />)
    expect(postMessage).toHaveBeenCalledWith({ command: 'getState' })
    state()
    expect(fetch).not.toHaveBeenCalled()
    fireEvent.click(screen.getByRole('button', { name: 'New Project' }))
    expect(postMessage).toHaveBeenLastCalledWith({ command: 'newProject' })
    expect(screen.getByRole('button', { name: 'Open Project' })).toBeDisabled()
    state()
    fireEvent.click(screen.getByRole('button', { name: 'Open Project' }))
    expect(postMessage).toHaveBeenLastCalledWith({ command: 'openProject' })
  })

  it('requires recovery and reports invalid project errors', () => {
    render(<App />)
    state({ recoveryAvailable: true, error: 'Project database is damaged.' })
    expect(screen.getByRole('alert')).toHaveTextContent('Project database is damaged.')
    expect(screen.getByRole('button', { name: 'New Project' })).toBeDisabled()
    fireEvent.click(screen.getByRole('button', { name: 'Recover Project' }))
    expect(postMessage).toHaveBeenLastCalledWith({ command: 'recoverProject' })
  })

  it('blocks the board on failed save and retries only saving', async () => {
    render(<App />)
    state({ projectName: 'Work.aiab', saveStatus: 'failed', error: 'Disk is full.' })
    expect(screen.getByText('Work.aiab')).toBeInTheDocument()
    expect(document.querySelector('[inert]')).not.toBeNull()
    expect(screen.getByRole('button', { name: 'Close Project' })).toBeDisabled()
    fireEvent.click(screen.getByRole('button', { name: 'Retry Save' }))
    expect(postMessage).toHaveBeenLastCalledWith({ command: 'retrySave' })
    state({ projectName: 'Work.aiab', saveStatus: 'saved' })
    expect(document.querySelector('[inert]')).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Close Project' }))
    expect(postMessage).toHaveBeenLastCalledWith({ command: 'closeProject' })
    state()
    expect(screen.getByRole('button', { name: 'Open Project' })).toBeEnabled()
    await screen.findByRole('heading', { name: 'AI Agile Board' })
  })

  it('sends settings to project storage', () => {
    updateProjectSettings({ theme: 'dark', zoom: 125 })
    expect(postMessage).toHaveBeenCalledWith({ command: 'updateSettings', settings: { theme: 'dark', zoom: 125 } })
  })
})

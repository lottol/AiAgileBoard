export const TicketType = {
  Epic: 'Epic',
  Feature: 'Feature',
  Story: 'Story',
  Task: 'Task',
} as const

export type TicketType = typeof TicketType[keyof typeof TicketType]

export type Ticket = {
  type: TicketType
  id: string
  title: string
  description: string
  comments: string[]
  storyPoints: number
  state: string
  humanNeeded: boolean
  assignee: 'Human' | 'Agent'
}

export const ticketStatuses = [
  'Backlog',
  'Ready for Human',
  'Human In Progress',
  'Waiting for Agent',
  'Agent In Progress',
  'Human Review',
  'Changes Requested',
  'Blocked',
  'Done',
  'Canceled',
]

export const agentStatuses = new Set(['Waiting for Agent', 'Agent In Progress', 'Changes Requested'])

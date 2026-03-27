# Help Ticket Workflow

This page explains the current help ticket flow for shard operators and staff reviewers. It starts with how players create tickets in-game, then describes how staff use the admin dashboard to triage and update them, and finishes with the HTTP API entry points that support the same workflow.

## Player Creation Flow

Players open a help ticket from the client help button. The client request enters Lua through `on_help_request(session_id, character_id)`, and the default help script opens a two-step gump flow:

1. A category picker is shown first.
2. After a category is chosen, a text-entry gump collects the ticket message.
3. Submitting the form calls `help_tickets.submit(session_id, category, message)`.

The shipped categories are:

- Question
- Stuck
- Bug
- Account
- Suggestion
- Other
- VerbalHarassment
- PhysicalHarassment

The Lua script trims the message before submission. On the server side, ticket creation only succeeds when the session is valid and linked to a live character and account. The ticket is then persisted with:

- the sender account and character
- the selected category
- the submitted message
- the sender location at the time of submission
- an initial `Open` status

Ticket creation also publishes a `TicketOpenedEvent` for downstream scripting or event handling.

## Staff Workflow

Staff review tickets in the admin dashboard under the help tickets page. The list view is designed for triage and shows:

- ticket id
- category
- status
- sender account and character
- map and coordinates
- created time
- assigned account

The list supports server-side pagination and filtering by:

- status
- category
- assigned to me

The detail view adds the full ticket message plus the assignment and status history fields currently stored by the server:

- assigned account and character
- assigned time
- closed time
- last updated time

From the detail page, staff can:

- take ownership of a ticket
- set the status to `Open`
- set the status to `Assigned`
- set the status to `Closed`

Current behavior to keep in mind:

- Taking ownership assigns the ticket to the authenticated staff account and marks the ticket `Assigned`.
- Status changes are immediate updates to the stored ticket record.
- Closing a ticket sets `ClosedAtUtc`.
- Reopening a ticket is allowed through the status action, but the current implementation does not clear a previously set closed timestamp.

Operationally, the dashboard is the place to triage and work tickets, while the player-side gump is only the ticket intake path. There is no separate in-game reply thread in the current implementation.

## HTTP API Mapping

The HTTP API mirrors the same workflow and is what the admin dashboard uses behind the scenes. Endpoint details are documented in [HTTP API](../architecture/http-api.md); this section only maps the route to the operator task it supports.

| Method | Path | Workflow use |
|--------|------|--------------|
| GET | `/api/help-tickets` | List tickets for staff triage, with pagination and filters |
| GET | `/api/help-tickets/{ticketId}` | Open one ticket in the staff detail view |
| PUT | `/api/help-tickets/{ticketId}/assign-to-me` | Take ownership of a ticket |
| PUT | `/api/help-tickets/{ticketId}/status` | Move a ticket between `Open`, `Assigned`, and `Closed` |
| GET | `/api/help-tickets/me` | Show the authenticated player's open or assigned tickets |

The `assigned to me` filter on the staff list and the `/me` endpoint both rely on the authenticated account. They are useful when a staff member wants to narrow the queue to their own work items or when a player needs to see the tickets that are still active.

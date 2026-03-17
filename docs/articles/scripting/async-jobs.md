# Async Jobs

`async_job` lets Lua request named background work without blocking the game loop.

## Purpose

Use `async_job` when a script needs:

- slow I/O
- expensive calculation
- snapshot-based planning
- external API work

Do not use it for direct world mutation from the worker.

## API

```lua
async_job.run(job_name, request_id, payload)
async_job.try_run(job_name, key, request_id, payload)
```

### `run`

Schedules a job immediately.

```lua
async_job.run("echo", "req-1", {
    text = "hello"
})
```

### `try_run`

Schedules a keyed job only if the same key is not already in flight.

```lua
local ok = async_job.try_run("echo", "npc:0x00123456", "req-2", {
    text = "scan"
})
```

If the same key is already running, it returns `false`.

## Callback Convention

Completion returns to Lua through global callbacks on the game loop:

```lua
function on_async_job_result(job_name, request_id, result)
end

function on_async_job_error(job_name, request_id, message)
end
```

`result` is a plain Lua table reconstructed from the background result payload.

Missing callbacks are ignored safely.

## Data Rules

Allowed payload/result value types:

- `nil`
- `boolean`
- `number`
- `string`
- nested tables with string keys
- sequential arrays

Not allowed:

- userdata
- functions
- threads
- live world objects

## Worker Safety

Background jobs must not mutate:

- mobiles
- items
- sessions
- sectors
- timers

Apply world changes only after the callback returns on the game loop.

## Example

```lua
function open_echo_demo()
    async_job.run("echo", "req-echo", {
        text = "hello world"
    })
end

function on_async_job_result(job_name, request_id, result)
    if job_name ~= "echo" or request_id ~= "req-echo" then
        return
    end

    log.info("Echo result: " .. tostring(result.payload.text))
end
```

## Built-In Jobs

Current built-in job names:

- `echo`

`echo` is mainly a reference job for testing the plumbing end to end.

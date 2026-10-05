-- Displays new book.content snapshots and legacy jail.text release notes.
-- The jail keeps jail.cell, jail.days and jail.fine as separate metadata.
jail_note = {}

function jail_note.on_use(serial, user)
    book.open(serial, user)
    return true
end

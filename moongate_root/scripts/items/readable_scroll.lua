-- Displays the plain text snapshot written when this document was created.
readable_scroll = {}

function readable_scroll.on_use(serial, user)
    book.open(serial, user)
    return true
end

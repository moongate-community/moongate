-- Opens the client's book on the text written on this item when it was created: its cover, then its pages.
readable_book = {}

function readable_book.on_use(serial, user)
    book.open(serial, user)
    return true
end

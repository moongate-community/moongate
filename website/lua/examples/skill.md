## check

Picking a lock that needs 30 points to try and never fails at 80; the try may raise the skill:

```lua
if skill.check(user, "lockpicking", 30, 80) then
    mobile.message(user, "The lock quickly yields to your skill.")
else
    mobile.message(user, "You are unable to pick the lock.")
end
```

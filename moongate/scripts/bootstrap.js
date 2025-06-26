const tst = require("./commands/test_cmd.js");

tst.lilli();

commands.registerCommand("echo", (c)=> {

    c.print(c.command);

    system.delay(1000);

    c.print("OK!")

}, "Echoes the input text back to the console", accountLevelType.User, commandSourceType.All);

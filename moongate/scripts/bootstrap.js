const tst = require("./commands/test_cmd.js");



const orionBrain = require("./ai/orion.js");

tst.lilli();

orionBrain.registerAi();

commands.registerCommand(
  "echo",
  (c) => {
    c.print(c.command);

    system.delay(1000);

    c.print("OK!");
  },
  "Echoes the input text back to the console",
  accountLevelType.User,
  commandSourceType.All,
);

events.onCharacterCreated((c) => {
  //public void AddItem(string templateId, ItemLayerType layer, UOMobileEntity mobile)
  c.context.addItem("inner_torso", itemLayerType.InnerTorso, c.mobile);
  c.context.addItem("outer_torso", itemLayerType.OuterTorso, c.mobile);
  c.context.addItem("one_hand", itemLayerType.OneHanded, c.mobile);
  c.context.addItem("middle_torso", itemLayerType.MiddleTorso, c.mobile);
  c.context.addItem("pants", itemLayerType.Pants, c.mobile);
  c.context.addItem("shoes", itemLayerType.Shoes, c.mobile);
  c.context.addItemToBackpack("brick", c.mobile);
});

const nc = class TestClass {
  constructor() {
    this.test = "test";
  }

  testMethod() {
    return "testMethod";
  }

  onUseItem(s, t) {}
};

items.addScriptToItem("test", new nc());

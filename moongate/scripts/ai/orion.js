const registerAi = () => {
  ai.addBrainAction("orion", (wrap) => {
    const list = ["Ho fame!", "Voglio i chicchini!", "Posciutto"];

    wrap.say(list[0]);
  });
};

export { registerAi };

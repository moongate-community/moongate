const registerAi = () => {
  ai.addBrainAction("orion", (wrap) => {
    const list = ["Ho fame!", "Voglio i chicchini!", "Posciutto"];

    const randomIndex = Math.floor(Math.random() * list.length);

    wrap.Say(list[randomIndex]);
  });
};

export { registerAi };

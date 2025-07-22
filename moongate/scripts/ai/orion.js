const registerAi = () => {
  ai.addBrainAction(
    "orion",
    (wrap) => {
      const list = ["Ho fame!", "Voglio i chicchini!", "Posciutto"];

      // const randomIndex = Math.floor(Math.random() * list.length);

      //wrap.Say(list[randomIndex]);
      wrap.Move(wrap.RandomDirection());
    },
    (context, text, mobile) => {
      context.Say("Ciao {0}!", mobile.Name);
    },
  );
};

export { registerAi };

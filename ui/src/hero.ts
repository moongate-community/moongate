import { heroui } from "@heroui/react";

export default heroui({
  defaultTheme: "dark",
  themes: {
    dark: {
      colors: {
        background: "#1f1c2a",
        foreground: "#f9f4ed",
        primary: {
          50:  "#e8f3fb",
          100: "#c5dff5",
          200: "#9dc8ee",
          300: "#7ab1e7",
          400: "#6aa5da",
          500: "#5594ca",
          600: "#4178a8",
          700: "#2f5d86",
          800: "#1f4366",
          900: "#132c48",
          DEFAULT: "#6aa5da",
          foreground: "#1f1c2a",
        },
        secondary: {
          DEFAULT: "#7675c3",
          foreground: "#f9f4ed",
        },
        content1: "#242130",
        content2: "#2c2940",
        content3: "#343455",
        content4: "#3c4d81",
        divider: "rgba(106, 165, 218, 0.2)",
        focus: "#6aa5da",
      },
    },
  },
});

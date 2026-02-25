import { heroui } from "@heroui/react";

export default heroui({
  defaultTheme: "dark",
  themes: {
    dark: {
      colors: {
        background: "#080c14",
        foreground: "#e2d9c8",
        primary: {
          50:  "#fef9ec",
          100: "#fdf0c4",
          200: "#fae08a",
          300: "#f7c94f",
          400: "#f4b529",
          500: "#f0a014",
          600: "#d4790d",
          700: "#b05710",
          800: "#8f4314",
          900: "#763814",
          DEFAULT: "#f0a014",
          foreground: "#080c14",
        },
        secondary: {
          DEFAULT: "#334155",
          foreground: "#e2d9c8",
        },
        content1: "#0d1220",
        content2: "#131929",
        content3: "#1a2235",
        content4: "#222d42",
        divider: "#1e2840",
        focus: "#f0a014",
      },
    },
  },
});

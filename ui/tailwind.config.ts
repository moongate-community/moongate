import type { Config } from 'tailwindcss'
import { heroui } from '@heroui/react'

export default {
  content: [
    './index.html',
    './src/**/*.{js,ts,jsx,tsx}',
    './node_modules/@heroui/theme/dist/**/*.{js,ts,jsx,tsx}',
  ],
  theme: {
    extend: {
      fontFamily: {
        cinzel: ['Cinzel', 'serif'],
        outfit: ['Outfit', 'sans-serif'],
        mono: ['JetBrains Mono', 'monospace'],
      },
      animation: {
        'glow-pulse': 'glow-pulse 3s ease-in-out infinite',
        'fade-in': 'fade-in 0.4s ease-out forwards',
      },
      keyframes: {
        'glow-pulse': {
          '0%, 100%': { opacity: '0.6' },
          '50%': { opacity: '1' },
        },
        'fade-in': {
          from: { opacity: '0', transform: 'translateY(6px)' },
          to: { opacity: '1', transform: 'translateY(0)' },
        },
      },
    },
  },
  darkMode: 'class',
  plugins: [
    heroui({
      defaultTheme: 'dark',
      themes: {
        dark: {
          colors: {
            background: '#080c14',
            foreground: '#e2d9c8',
            primary: {
              50:  '#fef9ec',
              100: '#fdf0c4',
              200: '#fae08a',
              300: '#f7c94f',
              400: '#f4b529',
              500: '#f0a014',
              600: '#d4790d',
              700: '#b05710',
              800: '#8f4314',
              900: '#763814',
              DEFAULT: '#f0a014',
              foreground: '#080c14',
            },
            secondary: {
              DEFAULT: '#334155',
              foreground: '#e2d9c8',
            },
            danger: {
              DEFAULT: '#ef4444',
              foreground: '#fff',
            },
            success: {
              DEFAULT: '#22c55e',
              foreground: '#fff',
            },
            content1: '#0d1220',
            content2: '#131929',
            content3: '#1a2235',
            content4: '#222d42',
            divider: '#1e2840',
            focus: '#f0a014',
          },
        },
      },
    }),
  ],
} satisfies Config

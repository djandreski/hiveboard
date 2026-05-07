/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  theme: {
    extend: {
      colors: {
        ink: {
          950: '#0a0d12',
          900: '#10141b',
          850: '#161b24',
          800: '#1c2230',
          700: '#283042',
          600: '#3a4258',
          500: '#5b6478',
          400: '#7a8398',
          300: '#a1aab8',
          200: '#d0d5de',
          100: '#eef0f4'
        },
        accent: {
          DEFAULT: '#6ea8ff',
          soft: '#1e2c44'
        }
      }
    }
  },
  plugins: []
}

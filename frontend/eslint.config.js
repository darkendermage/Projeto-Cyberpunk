import js from '@eslint/js'
import { defineConfig, globalIgnores } from 'eslint/config'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import tseslint from 'typescript-eslint'

export default defineConfig([
  globalIgnores(['dist']),
  {
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs.flat.recommended,
    ],
    files: ['**/*.{ts,tsx}'],
    languageOptions: {
      ecmaVersion: 2022,
      globals: globals.browser,
    },
    plugins: {
      'react-refresh': reactRefresh,
    },
    rules: {
      // Several inputs and filter panels copy a prop into a local draft in an effect (FilterPanel,
      // NumberFilter, NoteInput...). Rewriting them to adjust state during render changes how they
      // interact with debouncing, so the React Compiler rule only warns until each one is revisited.
      'react-hooks/set-state-in-effect': 'warn',
      'react-refresh/only-export-components': [
        'warn',
        { allowConstantExport: true },
      ],
    },
  },
])

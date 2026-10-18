import { defineConfig } from 'vitest/config';

// Unit tests cover pure logic (filter model, URL encoding), so no DOM or React plugin is needed.
export default defineConfig({
  test: {
    environment: 'node',
    include: ['src/**/*.test.ts'],
  },
});

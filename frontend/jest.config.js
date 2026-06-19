/**
 * Jest configuration for the Pulsar frontend.
 *
 * Unit tests run on jsdom via jest-preset-angular (the Angular team's supported
 * Jest integration). `roots` is pinned to `src` so Jest never picks up the
 * Playwright E2E specs under `e2e/`, which share the `*.spec.ts` suffix but use
 * a different runner.
 */
module.exports = {
  preset: 'jest-preset-angular',
  setupFilesAfterEnv: ['<rootDir>/setup-jest.ts'],
  roots: ['<rootDir>/src'],
  testPathIgnorePatterns: ['<rootDir>/node_modules/', '<rootDir>/dist/', '<rootDir>/e2e/'],
  collectCoverageFrom: [
    'src/**/*.ts',
    '!src/**/*.spec.ts',
    '!src/main.ts',
    '!src/**/*.d.ts',
    '!src/testing/**',
  ],
  coverageDirectory: '<rootDir>/coverage',
};

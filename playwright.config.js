const { defineConfig, devices } = require('@playwright/test');
if (!process.env.TEST_DB_URL) throw new Error('Set TEST_DB_URL to an isolated PostgreSQL database for browser tests.');
module.exports = defineConfig({
  testDir: './tests/browser', fullyParallel: false, workers: 1,
  use: { baseURL: 'http://127.0.0.1:5199', trace: 'retain-on-failure' },
  projects: [{ name: 'desktop', use: { ...devices['Desktop Chrome'] } }, { name: 'mobile', use: { ...devices['Pixel 7'] } }],
  webServer: {
    command: 'dotnet run --project JASIntWeb --no-launch-profile --configuration Release --no-build --urls http://127.0.0.1:5199',
    url: 'http://127.0.0.1:5199/health', timeout: 120000, reuseExistingServer: false,
    env: {
      DATABASE_URL: process.env.TEST_DB_URL, DB_URL: process.env.TEST_DB_URL,
      ASPNETCORE_ENVIRONMENT: 'Development', DOTNET_ENVIRONMENT: 'Development', Seed__Enabled: 'true',
      Admin__Email: 'browser.admin@example.test', Admin__Password: 'BrowserTest123!',
      Mail__Provider: 'Smtp', Mail__Host: '', Mail__Smtp__Host: '',
      CLOUDINARY_URL: '', Cloudinary__Url: '', Cloudinary__CloudName: '', CLOUDINARY_CLOUD_NAME: '',
      PublicBaseUrl: 'http://127.0.0.1:5199', Meta__AccessToken: '', META_ACCESS_TOKEN: ''
    }
  }
});

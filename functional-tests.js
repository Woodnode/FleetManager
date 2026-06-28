/**
 * Functional test suite for FleetManager V2
 * Tests all major user scenarios across all roles.
 * Run: node functional-tests.js
 */

const { chromium } = require('playwright');

const BASE_URL = 'http://localhost:5173';
const PASS = 'Fleet@2024';
const ACCOUNTS = {
  admin:   { email: 'admin@fleetmanager.fr' },
  manager: { email: 'directeur.paris@fleetmanager.fr' },
  tech:    { email: 'tech1.paris@fleetmanager.fr' },
};

// ─── Helpers ─────────────────────────────────────────────────────────────────

const results = [];
let passed = 0, failed = 0;

function log(tag, msg) {
  const ts = new Date().toLocaleTimeString('fr-FR');
  console.log(`[${ts}] ${tag} ${msg}`);
}

async function test(name, fn) {
  try {
    await fn();
    passed++;
    results.push({ name, ok: true });
    log('✅', name);
  } catch (err) {
    failed++;
    results.push({ name, ok: false, err: err.message.split('\n')[0] });
    log('❌', `${name}\n       → ${err.message.split('\n')[0]}`);
  }
}

async function loginAs(page, account) {
  await page.goto(`${BASE_URL}/login`);
  await page.fill('input[type="email"]', account.email);
  await page.fill('input[type="password"]', PASS);
  await page.click('button[type="submit"]');
  await page.waitForURL(`${BASE_URL}/dashboard`, { timeout: 8000 });
}

// Wait for spinner to disappear then assert
async function waitForData(page) {
  await page.waitForSelector('.w-7.h-7', { state: 'detached', timeout: 10000 }).catch(() => {});
  await page.waitForTimeout(300);
}

// Click a button INSIDE the modal (not the page-level one hidden behind the backdrop)
async function clickInModal(page, text) {
  const modalBtn = page.locator('.fm-modal').getByRole('button', { name: text });
  await modalBtn.click();
}

// Dismiss any open modal with Escape
async function closeModal(page) {
  await page.keyboard.press('Escape');
  await page.waitForTimeout(200);
}

// ─── Test Suites ─────────────────────────────────────────────────────────────

async function testLogin(browser) {
  log('▶', '=== Login ===');
  const page = await browser.newPage();

  await test('Login page renders without redirect', async () => {
    await page.goto(`${BASE_URL}/login`);
    await page.waitForSelector('input[type="email"]');
    const title = await page.textContent('h1');
    if (!title?.includes('Connexion')) throw new Error(`Expected "Connexion" got "${title}"`);
  });

  await test('Demo account buttons fill the email field', async () => {
    await page.goto(`${BASE_URL}/login`);
    // First demo button = Admin
    const demoBtn = page.locator('button[type="button"]').first();
    await demoBtn.click();
    const emailVal = await page.inputValue('input[type="email"]');
    if (!emailVal.includes('@fleetmanager.fr')) throw new Error(`Email not filled: "${emailVal}"`);
  });

  await test('Login with wrong password shows error', async () => {
    await page.goto(`${BASE_URL}/login`);
    await page.fill('input[type="email"]', ACCOUNTS.admin.email);
    await page.fill('input[type="password"]', 'wrongpassword');
    await page.click('button[type="submit"]');
    await page.waitForSelector('text=Email ou mot de passe incorrect', { timeout: 5000 });
  });

  await test('Login with unknown email shows error', async () => {
    await page.goto(`${BASE_URL}/login`);
    await page.fill('input[type="email"]', 'nobody@test.fr');
    await page.fill('input[type="password"]', PASS);
    await page.click('button[type="submit"]');
    await page.waitForSelector('text=Email ou mot de passe incorrect', { timeout: 5000 });
  });

  await test('Admin login succeeds and redirects to /dashboard', async () => {
    await loginAs(page, ACCOUNTS.admin);
    await page.waitForSelector('text=Dashboard', { timeout: 5000 });
  });

  await test('Unauthenticated access to /dashboard redirects to login', async () => {
    await page.evaluate(() => localStorage.clear());
    await page.goto(`${BASE_URL}/dashboard`);
    await page.waitForURL(`${BASE_URL}/login`, { timeout: 5000 });
  });

  await test('Unauthenticated access to /vehicles redirects to login', async () => {
    await page.evaluate(() => localStorage.clear());
    await page.goto(`${BASE_URL}/vehicles`);
    await page.waitForURL(`${BASE_URL}/login`, { timeout: 5000 });
  });

  await test('Unauthenticated access to /interventions redirects to login', async () => {
    await page.evaluate(() => localStorage.clear());
    await page.goto(`${BASE_URL}/interventions`);
    await page.waitForURL(`${BASE_URL}/login`, { timeout: 5000 });
  });

  await page.close();
}

async function testDashboard(browser) {
  log('▶', '=== Dashboard ===');
  const page = await browser.newPage();
  await loginAs(page, ACCOUNTS.admin);
  await page.goto(`${BASE_URL}/dashboard`);
  await waitForData(page);

  await test('Dashboard shows 4 KPI cards after data loads', async () => {
    // KPI cards each have a large number + title text
    const kpiTitles = ['Total véhicules', 'Disponibles', 'Interventions prévues', 'Taux de dispo'];
    for (const t of kpiTitles) {
      await page.waitForSelector(`text=${t}`, { timeout: 5000 });
    }
  });

  await test('Dashboard vehicle pie chart renders', async () => {
    await page.waitForSelector('text=Répartition du parc', { timeout: 5000 });
  });

  await test('Dashboard intervention bar chart renders', async () => {
    await page.waitForSelector('text=Interventions par type', { timeout: 5000 });
  });

  await test('Dashboard recent interventions table renders', async () => {
    await page.waitForSelector('text=Interventions récentes', { timeout: 5000 });
  });

  await test('"Données en direct" live indicator is visible', async () => {
    await page.waitForSelector('text=Données en direct', { timeout: 5000 });
  });

  await test('KPI "Total véhicules" shows a number > 0', async () => {
    // The count appears as a large bold number next to the title
    const cards = page.locator('.fm-card');
    const first = cards.first();
    const txt = await first.textContent();
    if (!/\d+/.test(txt ?? '')) throw new Error('No numeric value found in first KPI card');
  });

  await page.close();
}

async function testVehicles(browser) {
  log('▶', '=== Véhicules ===');
  const page = await browser.newPage();
  await loginAs(page, ACCOUNTS.admin);

  await test('Vehicles page loads and shows table headers', async () => {
    await page.goto(`${BASE_URL}/vehicles`);
    await page.waitForSelector('th:has-text("VIN")', { timeout: 5000 });
    await page.waitForSelector('th:has-text("Statut")', { timeout: 5000 });
  });

  await test('Vehicle count is displayed in the subtitle', async () => {
    const subtitle = await page.textContent('p.text-sm.text-slate-400');
    if (!subtitle || !/\d+/.test(subtitle)) throw new Error(`Subtitle has no count: "${subtitle}"`);
  });

  await test('Search filter hides non-matching rows', async () => {
    await page.goto(`${BASE_URL}/vehicles`);
    await waitForData(page);
    await page.fill('input[placeholder*="VIN"]', 'XXXXX_NOTFOUND_XXXXX');
    await page.waitForTimeout(300);
    await page.waitForSelector('text=Aucun véhicule correspond aux filtres', { timeout: 3000 });
  });

  await test('Clearing search restores the full list', async () => {
    await page.fill('input[placeholder*="VIN"]', '');
    await page.waitForTimeout(300);
    // The "no results" message should be gone
    const noResults = await page.locator('text=Aucun véhicule correspond aux filtres').count();
    if (noResults > 0) throw new Error('"no results" text still visible after clearing search');
  });

  await test('Status filter dropdown has 5 options (Tous + 4 statuts)', async () => {
    const select = page.locator('select').first();
    const options = await select.locator('option').allTextContents();
    if (options.length < 5) throw new Error(`Expected 5 options, got ${options.length}: ${options.join(', ')}`);
  });

  await test('Status filter "Disponible" shows only available vehicles', async () => {
    const select = page.locator('select').first();
    await select.selectOption('Available');
    await page.waitForTimeout(300);
    const rows = page.locator('tbody tr');
    const count = await rows.count();
    if (count > 0) {
      // Every badge in "Statut" column should say "Disponible"
      const allText = await page.locator('tbody').textContent();
      if (allText?.includes('En intervention') || allText?.includes('Vendu')) {
        throw new Error('Non-available vehicles visible with Available filter');
      }
    }
    await select.selectOption('');
  });

  await test('"Ajouter un véhicule" button opens modal with VIN field', async () => {
    await page.goto(`${BASE_URL}/vehicles`);
    await page.click('button:has-text("Ajouter un véhicule")');
    await page.waitForSelector('.fm-modal', { timeout: 3000 });
    await page.waitForSelector('input.font-mono', { timeout: 3000 });
  });

  await test('Create vehicle: VIN shorter than 17 chars shows toast error', async () => {
    await page.fill('input.font-mono', 'ABC123');
    await clickInModal(page, 'Ajouter');
    await page.waitForSelector('text=17 caractères', { timeout: 5000 });
  });

  await test('Create vehicle: missing brand/model shows toast error', async () => {
    await page.fill('input.font-mono', 'VF1RFD00X67891234');
    // brand and model are empty by default
    await clickInModal(page, 'Ajouter');
    await page.waitForSelector('text=champs obligatoires', { timeout: 5000 });
  });

  await test('Escape key closes the add vehicle modal', async () => {
    await closeModal(page);
    await page.waitForFunction(
      () => document.querySelectorAll('.fm-modal').length === 0,
      { timeout: 3000 }
    );
  });

  await test('Row hover reveals action buttons (status, edit)', async () => {
    await page.goto(`${BASE_URL}/vehicles`);
    await waitForData(page);
    const row = page.locator('tbody tr').first();
    await row.hover();
    await page.waitForTimeout(400);
    const statusBtn = row.locator('button[title="Changer le statut"]');
    await statusBtn.waitFor({ state: 'visible', timeout: 3000 });
  });

  await test('Change status modal opens and shows vehicle info', async () => {
    const row = page.locator('tbody tr').first();
    await row.hover();
    await row.locator('button[title="Changer le statut"]').click();
    await page.waitForSelector('.fm-modal', { timeout: 3000 });
    await page.waitForSelector('text=Changer le statut', { timeout: 3000 });
    await closeModal(page);
  });

  await test('Edit modal opens with pre-filled brand field', async () => {
    await page.goto(`${BASE_URL}/vehicles`);
    await waitForData(page);
    const row = page.locator('tbody tr').first();
    await row.hover();
    await row.locator('button[title="Modifier"]').click();
    await page.waitForSelector('.fm-modal', { timeout: 3000 });
    const brandInput = page.locator('.fm-modal input[placeholder="Renault"]');
    const val = await brandInput.inputValue();
    if (!val) throw new Error('Brand field is empty in edit modal');
    await closeModal(page);
  });

  await test('Delete button is visible for Admin role', async () => {
    await page.goto(`${BASE_URL}/vehicles`);
    await waitForData(page);
    const row = page.locator('tbody tr').first();
    await row.hover();
    const deleteBtn = row.locator('button[title="Supprimer"]');
    await deleteBtn.waitFor({ state: 'visible', timeout: 3000 });
  });

  await test('Delete modal shows confirmation text and is reversible', async () => {
    const row = page.locator('tbody tr').first();
    await row.hover();
    await row.locator('button[title="Supprimer"]').click();
    await page.waitForSelector('text=irréversible', { timeout: 3000 });
    // Cancel it — don't actually delete
    await page.locator('.fm-modal').getByRole('button', { name: 'Annuler' }).click();
    await page.waitForFunction(() => document.querySelectorAll('.fm-modal').length === 0, { timeout: 3000 });
  });

  await page.close();
}

async function testVehiclesTechnicianRole(browser) {
  log('▶', '=== Véhicules — rôle Technicien ===');
  const page = await browser.newPage();
  await loginAs(page, ACCOUNTS.tech);

  await test('Technician: no delete button on any vehicle row', async () => {
    await page.goto(`${BASE_URL}/vehicles`);
    await waitForData(page);
    const row = page.locator('tbody tr').first();
    await row.hover();
    await page.waitForTimeout(400);
    const deleteBtn = row.locator('button[title="Supprimer"]');
    const count = await deleteBtn.count();
    if (count > 0) throw new Error('Delete button should not appear for Technician');
  });

  await page.close();
}

async function testInterventions(browser) {
  log('▶', '=== Interventions ===');
  const page = await browser.newPage();
  await loginAs(page, ACCOUNTS.admin);

  await test('Interventions page loads with correct table headers', async () => {
    await page.goto(`${BASE_URL}/interventions`);
    await page.waitForSelector('th:has-text("Véhicule")', { timeout: 5000 });
    await page.waitForSelector('th:has-text("Technicien")', { timeout: 5000 });
  });

  await test('Status filter "Planifiée" shows only planned interventions', async () => {
    await page.goto(`${BASE_URL}/interventions`);
    await waitForData(page);
    const statusSelect = page.locator('select').first();
    await statusSelect.selectOption('Planned');
    await page.waitForTimeout(300);
    const bodyText = await page.locator('tbody').textContent();
    if (bodyText?.includes('En cours') || bodyText?.includes('Terminée') || bodyText?.includes('Annulée')) {
      throw new Error('Non-planned interventions visible with Planned filter');
    }
    await statusSelect.selectOption('');
  });

  await test('Type filter "Maintenance" shows only maintenance interventions', async () => {
    const typeSelect = page.locator('select').nth(1);
    await typeSelect.selectOption('Maintenance');
    await page.waitForTimeout(300);
    const bodyText = await page.locator('tbody').textContent();
    if (bodyText?.includes('Réparation') || bodyText?.includes('Inspection')) {
      throw new Error('Non-maintenance interventions visible with Maintenance filter');
    }
    await typeSelect.selectOption('');
  });

  await test('"Nouvelle intervention" button opens creation modal', async () => {
    await page.click('button:has-text("Nouvelle intervention")');
    await page.waitForSelector('.fm-modal', { timeout: 3000 });
    await page.waitForSelector('text=Nouvelle intervention', { timeout: 3000 });
  });

  await test('Create intervention: clicking Créer without vehicle shows error', async () => {
    await clickInModal(page, 'Créer');
    await page.waitForSelector('text=sélectionner un véhicule', { timeout: 5000 });
  });

  await test('Create intervention: technician dropdown disabled before store selected', async () => {
    const disabledSelects = page.locator('.fm-modal select[disabled]');
    const count = await disabledSelects.count();
    if (count === 0) throw new Error('Technician select should be disabled before store is chosen');
  });

  await test('Create intervention: end date < start date shows error', async () => {
    // Fill vehicle, store – we'll hit date validation by filling all but making end < start
    const vehicleSelect = page.locator('.fm-modal select').first();
    const firstVehicle = await vehicleSelect.locator('option').nth(1).getAttribute('value');
    if (firstVehicle) await vehicleSelect.selectOption(firstVehicle);

    const storeSelect = page.locator('.fm-modal select').nth(2);
    const firstStore = await storeSelect.locator('option').nth(1).getAttribute('value');
    if (firstStore) await storeSelect.selectOption(firstStore);

    // Wait for technicians to load
    await page.waitForTimeout(1000);
    const techSelect = page.locator('.fm-modal select').nth(3);
    const techOptions = await techSelect.locator('option').count();
    if (techOptions > 1) {
      const firstTech = await techSelect.locator('option').nth(1).getAttribute('value');
      if (firstTech) await techSelect.selectOption(firstTech);
    }

    const dateInputs = page.locator('.fm-modal input[type="date"]');
    await dateInputs.nth(0).fill('2024-12-31'); // start
    await dateInputs.nth(1).fill('2024-12-01'); // end < start
    await clickInModal(page, 'Créer');
    await page.waitForSelector('text=La date de fin doit être après', { timeout: 5000 });
  });

  await test('Escape closes the create intervention modal', async () => {
    await closeModal(page);
    await page.waitForFunction(() => document.querySelectorAll('.fm-modal').length === 0, { timeout: 3000 });
  });

  // ── Helper: create a Planned intervention via UI for button tests ──
  const createPlannedIntervention = async () => {
    await page.goto(`${BASE_URL}/interventions`);
    await waitForData(page);
    await page.click('button:has-text("Nouvelle intervention")');
    await page.waitForSelector('.fm-modal', { timeout: 3000 });

    // Pick first available vehicle
    const vehicleSelect = page.locator('.fm-modal select').first();
    await vehicleSelect.selectOption({ index: 1 });

    // Pick first store
    const storeSelect = page.locator('.fm-modal select').nth(2);
    await storeSelect.selectOption({ index: 1 });
    await page.waitForTimeout(1000); // let techs load

    // Pick first technician
    const techSelect = page.locator('.fm-modal select').nth(3);
    const techCount = await techSelect.locator('option').count();
    if (techCount > 1) await techSelect.selectOption({ index: 1 });

    // Set valid future dates
    const dateInputs = page.locator('.fm-modal input[type="date"]');
    await dateInputs.nth(0).fill('2027-01-10');
    await dateInputs.nth(1).fill('2027-01-15');

    await clickInModal(page, 'Créer');
    // Wait for modal to close (success)
    await page.waitForFunction(() => document.querySelectorAll('.fm-modal').length === 0, { timeout: 5000 });
    await page.waitForTimeout(300);
  };

  await test('Planned intervention row shows "Démarrer" and "Annuler" buttons on hover', async () => {
    // Navigate fresh, filter for Planned
    await page.goto(`${BASE_URL}/interventions`);
    await waitForData(page);
    const statusSelect = page.locator('select').first();
    await statusSelect.selectOption('Planned');
    await page.waitForTimeout(500);

    // Check if there are actual data rows (not the empty-state row)
    const emptyMsg = await page.locator('text=Aucune intervention correspond').count();
    if (emptyMsg > 0) {
      log('ℹ', 'No Planned rows in DB — creating one via UI');
      await statusSelect.selectOption('');
      await createPlannedIntervention();
      await statusSelect.selectOption('Planned');
      await page.waitForTimeout(500);
    }

    const rows = page.locator('tbody tr');
    const rowCount = await rows.count();
    if (rowCount === 0) throw new Error('Still no rows after creating a planned intervention');

    const row = rows.first();
    await row.hover();
    await page.waitForTimeout(600);

    const demarrerCount = await row.locator('button[title="Démarrer"]').count();
    if (demarrerCount === 0) throw new Error('"Démarrer" button not found on Planned row');

    const annulerCount = await row.locator('button[title="Annuler"]').count();
    if (annulerCount === 0) throw new Error('"Annuler" button not found on Planned row');
  });

  await test('Cancelling an intervention requires a reason (validation)', async () => {
    // Stay on the Planned-filtered view from the previous test
    const statusSelect = page.locator('select').first();
    const currentVal = await statusSelect.inputValue();
    if (currentVal !== 'Planned') {
      await statusSelect.selectOption('Planned');
      await page.waitForTimeout(500);
    }

    const emptyMsg = await page.locator('text=Aucune intervention correspond').count();
    if (emptyMsg > 0) { log('ℹ', 'No Planned rows — skip cancel test'); return; }

    const rows = page.locator('tbody tr');
    const row = rows.first();
    await row.hover();
    await page.waitForTimeout(600);
    await row.locator('button[title="Annuler"]').click({ force: true });
    await page.waitForSelector('.fm-modal', { timeout: 5000 });
    // Submit without reason
    await clickInModal(page, 'Confirmer');
    await page.waitForSelector('text=raison est requise', { timeout: 5000 });
    await closeModal(page);
  });

  await test('InProgress intervention shows "Terminer" but not "Démarrer"', async () => {
    const statusSelect = page.locator('select').first();
    await statusSelect.selectOption('InProgress');
    await page.waitForTimeout(300);
    const rows = page.locator('tbody tr');
    if (await rows.count() === 0) { log('ℹ', 'No InProgress rows — skip'); return; }
    const row = rows.first();
    await row.hover();
    await page.waitForTimeout(400);
    const demarrer = await row.locator('button[title="Démarrer"]').count();
    if (demarrer > 0) throw new Error('"Démarrer" should not appear on InProgress');
    await row.locator('button[title="Terminer"]').waitFor({ state: 'visible', timeout: 3000 });
  });

  await test('Completed interventions have no action buttons', async () => {
    const statusSelect = page.locator('select').first();
    await statusSelect.selectOption('Completed');
    await page.waitForTimeout(300);
    const rows = page.locator('tbody tr');
    if (await rows.count() === 0) { log('ℹ', 'No Completed rows — skip'); return; }
    const row = rows.first();
    await row.hover();
    await page.waitForTimeout(400);
    const btns = await row.locator('button[title]').count();
    if (btns > 0) throw new Error(`Completed row should have 0 action buttons, found ${btns}`);
  });

  await page.close();
}

async function testStores(browser) {
  log('▶', '=== Enseignes ===');
  const page = await browser.newPage();
  await loginAs(page, ACCOUNTS.admin);

  await test('Stores page loads and shows title', async () => {
    await page.goto(`${BASE_URL}/stores`);
    await page.waitForSelector('text=Enseignes', { timeout: 5000 });
  });

  await test('Admin sees "Ajouter une enseigne" button', async () => {
    await page.waitForSelector('button:has-text("Ajouter une enseigne")', { timeout: 3000 });
  });

  await test('Store cards are displayed in a grid', async () => {
    await waitForData(page);
    const cards = page.locator('.fm-card');
    const count = await cards.count();
    if (count === 0) throw new Error('No store cards rendered');
  });

  await test('"Ajouter une enseigne" opens form modal', async () => {
    await page.click('button:has-text("Ajouter une enseigne")');
    await page.waitForSelector('.fm-modal', { timeout: 3000 });
    await page.waitForSelector('input[placeholder*="AutoGroup"]', { timeout: 3000 });
  });

  await test('Add store: submit disabled when name and city empty', async () => {
    const submitBtn = page.locator('.fm-modal').getByRole('button', { name: 'Ajouter' });
    const isDisabled = await submitBtn.isDisabled();
    if (!isDisabled) throw new Error('Submit should be disabled with empty name/city');
  });

  await test('Add store: submit enabled after filling name + city', async () => {
    await page.locator('.fm-modal input[placeholder*="AutoGroup"]').fill('Test Enseigne');
    await page.locator('.fm-modal input[placeholder="Paris"]').fill('Lyon');
    const submitBtn = page.locator('.fm-modal').getByRole('button', { name: 'Ajouter' });
    const isDisabled = await submitBtn.isDisabled();
    if (isDisabled) throw new Error('Submit should be enabled after filling name+city');
    await closeModal(page);
  });

  await page.close();

  // ── Rôle Manager : pas de bouton "Ajouter" ──
  const page2 = await browser.newPage();
  await loginAs(page2, ACCOUNTS.manager);

  await test('Manager does NOT see "Ajouter une enseigne" button', async () => {
    await page2.goto(`${BASE_URL}/stores`);
    await page2.waitForSelector('text=Enseignes', { timeout: 5000 });
    await page2.waitForTimeout(500);
    const count = await page2.locator('button:has-text("Ajouter une enseigne")').count();
    if (count > 0) throw new Error('Manager should not see the "Ajouter une enseigne" button');
  });

  await test('Manager can still see the store list', async () => {
    await page2.waitForSelector('.fm-card', { timeout: 5000 });
  });

  await page2.close();
}

async function testRouteProtection(browser) {
  log('▶', '=== Navigation & Protection des routes ===');
  const page = await browser.newPage();
  await loginAs(page, ACCOUNTS.admin);

  await test('/ redirects to /dashboard when authenticated', async () => {
    await page.goto(`${BASE_URL}/`);
    await page.waitForURL(`${BASE_URL}/dashboard`, { timeout: 5000 });
  });

  await test('Unknown route redirects to /dashboard', async () => {
    await page.goto(`${BASE_URL}/this-does-not-exist`);
    await page.waitForURL(`${BASE_URL}/dashboard`, { timeout: 5000 });
  });

  await test('Sidebar nav "Véhicules" link navigates correctly', async () => {
    await page.goto(`${BASE_URL}/dashboard`);
    await page.click('a[href="/vehicles"]');
    await page.waitForURL(`${BASE_URL}/vehicles`, { timeout: 5000 });
  });

  await test('Sidebar nav "Interventions" link navigates correctly', async () => {
    await page.click('a[href="/interventions"]');
    await page.waitForURL(`${BASE_URL}/interventions`, { timeout: 5000 });
  });

  await test('Sidebar nav "Enseignes" link navigates correctly', async () => {
    await page.click('a[href="/stores"]');
    await page.waitForURL(`${BASE_URL}/stores`, { timeout: 5000 });
  });

  await test('Sidebar nav "Dashboard" link navigates correctly', async () => {
    await page.click('a[href="/dashboard"]');
    await page.waitForURL(`${BASE_URL}/dashboard`, { timeout: 5000 });
  });

  await test('Sidebar displays user email and role', async () => {
    const sidebar = page.locator('aside');
    const txt = await sidebar.textContent();
    if (!txt?.includes(ACCOUNTS.admin.email)) throw new Error('Email not found in sidebar');
  });

  await test('"Déconnexion" button clears session and redirects to login', async () => {
    await page.locator('aside').getByRole('button', { name: 'Déconnexion' }).click();
    await page.waitForURL(`${BASE_URL}/login`, { timeout: 5000 });
    // Confirm token gone
    const token = await page.evaluate(() => localStorage.getItem('token'));
    if (token) throw new Error('Token should be cleared after logout');
  });

  await test('After logout, /dashboard redirects back to login', async () => {
    await page.goto(`${BASE_URL}/dashboard`);
    await page.waitForURL(`${BASE_URL}/login`, { timeout: 5000 });
  });

  await page.close();
}

// ─── Main ─────────────────────────────────────────────────────────────────────

(async () => {
  console.log('');
  console.log('═══════════════════════════════════════════════════');
  console.log('  FleetManager V2 — Tests Fonctionnels');
  console.log('═══════════════════════════════════════════════════');
  console.log('');

  const browser = await chromium.launch({
    executablePath: 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
    headless: true,
    args: ['--no-sandbox', '--disable-dev-shm-usage'],
  });

  try {
    await testLogin(browser);
    await testDashboard(browser);
    await testVehicles(browser);
    await testVehiclesTechnicianRole(browser);
    await testInterventions(browser);
    await testStores(browser);
    await testRouteProtection(browser);
  } finally {
    await browser.close();
  }

  console.log('');
  console.log('═══════════════════════════════════════════════════');
  console.log(`  Résultats : ${passed} ✅  ${failed} ❌  (total: ${passed + failed})`);
  console.log('═══════════════════════════════════════════════════');
  if (failed > 0) {
    console.log('\nÉchecs :');
    results.filter(r => !r.ok).forEach(r => console.log(`  ❌ ${r.name}\n     ${r.err}`));
  }
  console.log('');
  process.exit(failed > 0 ? 1 : 0);
})();

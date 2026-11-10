const crypto = require('crypto');
const fs = require('fs');
const os = require('os');
const path = require('path');
const { execSync } = require('child_process');

const ROOT_DIR = path.join(__dirname, '..');
const DATA_DIR = path.join(ROOT_DIR, 'data');
const LICENSE_FILE = path.join(DATA_DIR, 'license.key');

// Secret cryptographic signing key (used to sign and verify hardware-locked licenses)
const SECRET_SIGNING_SALT = 'RUPESH_OCR_SUITE_HW_LOCK_2026_#9981_SECRET_KEY';
// Master Admin Password for Rupesh when installing directly on a client laptop
const MASTER_ADMIN_PIN_HASH = crypto
  .createHash('sha256')
  .update('Rupesh@2026' + SECRET_SIGNING_SALT)
  .digest('hex');

let cachedMachineId = null;

/**
 * Reads the unique Windows MachineGuid from Registry + CPU/Host fingerprint
 * so the license is strictly locked to this physical Windows installation & hardware.
 */
function getHardwareMachineId() {
  if (cachedMachineId) return cachedMachineId;

  let rawGuid = '';
  try {
    const regOut = execSync(
      'reg query "HKLM\\SOFTWARE\\Microsoft\\Cryptography" /v MachineGuid',
      { encoding: 'utf8', windowsHide: true, stdio: ['ignore', 'pipe', 'ignore'] }
    );
    const match = regOut.match(/MachineGuid\s+REG_SZ\s+([A-Za-z0-9-]+)/i);
    if (match && match[1]) {
      rawGuid = match[1].trim();
    }
  } catch (_) {}

  if (!rawGuid) {
    const cpus = os.cpus() || [];
    const cpuModel = cpus[0] ? cpus[0].model : 'generic-cpu';
    rawGuid = `${os.hostname()}|${os.platform()}|${os.arch()}|${cpuModel}|${os.totalmem()}`;
  }

  const hash = crypto
    .createHash('sha256')
    .update(rawGuid.toUpperCase() + '|WIN_HW_ID')
    .digest('hex')
    .toUpperCase();

  cachedMachineId = `RUP-${hash.slice(0, 4)}-${hash.slice(4, 8)}-${hash.slice(8, 12)}`;
  return cachedMachineId;
}

/**
 * Generates a cryptographic License Key for a given target Machine ID
 * Format: LIC-<MACHINE_ID_CLEAN>-<EXPIRY_YYYYMMDD_OR_LIFETIME>-<SIGNATURE_12>
 */
function generateLicenseKeyForMachine(targetMachineId, expiryCode = 'LIFETIME') {
  const normMachine = String(targetMachineId || '')
    .trim()
    .toUpperCase();
  const normExpiry = String(expiryCode || 'LIFETIME')
    .trim()
    .toUpperCase();

  const payload = `${normMachine}|${normExpiry}`;
  const sig = crypto
    .createHmac('sha256', SECRET_SIGNING_SALT)
    .update(payload)
    .digest('hex')
    .toUpperCase()
    .slice(0, 12);

  const cleanMid = normMachine.replace(/^RUP-/, '').replace(/-/g, '');
  return `LIC-${cleanMid}-${normExpiry}-${sig}`;
}

/**
 * Verifies whether a license key string is valid for the current laptop's Machine ID
 */
function verifyLicenseKey(licenseKeyStr, expectedMachineId = null) {
  const machineId = (expectedMachineId || getHardwareMachineId()).trim().toUpperCase();
  if (!licenseKeyStr || typeof licenseKeyStr !== 'string') {
    return { valid: false, reason: 'No license key found on this machine.' };
  }

  const parts = licenseKeyStr.trim().toUpperCase().split('-');
  // Expected: LIC - <12_HEX_MID> - <EXPIRY> - <12_HEX_SIG>
  if (parts.length !== 4 || parts[0] !== 'LIC') {
    return { valid: false, reason: 'Invalid license key format.' };
  }

  const keyMidClean = parts[1];
  const expiryCode = parts[2];
  const providedSig = parts[3];

  const thisMidClean = machineId.replace(/^RUP-/, '').replace(/-/g, '');
  if (keyMidClean !== thisMidClean) {
    return {
      valid: false,
      reason: `Hardware Mismatch: License belongs to another machine, not this laptop (${machineId}).`
    };
  }

  const expectedKey = generateLicenseKeyForMachine(machineId, expiryCode);
  const expectedSig = expectedKey.split('-')[3];

  if (providedSig !== expectedSig) {
    return { valid: false, reason: 'Invalid license signature.' };
  }

  // Check expiry if not LIFETIME (format YYYYMMDD)
  let expiresAt = 'Lifetime (Never Expires)';
  let daysRemaining = 99999;

  if (expiryCode !== 'LIFETIME') {
    if (!/^\d{8}$/.test(expiryCode)) {
      return { valid: false, reason: 'Invalid license expiry date.' };
    }
    const y = parseInt(expiryCode.slice(0, 4), 10);
    const m = parseInt(expiryCode.slice(4, 6), 10) - 1;
    const d = parseInt(expiryCode.slice(6, 8), 10);
    const expDate = new Date(y, m, d, 23, 59, 59);
    const now = new Date();
    if (now > expDate) {
      return {
        valid: false,
        reason: `License expired on ${expiryCode.slice(0, 4)}-${expiryCode.slice(4, 6)}-${expiryCode.slice(6, 8)}. Please contact Rupesh Singh to renew.`
      };
    }
    expiresAt = `${expiryCode.slice(0, 4)}-${expiryCode.slice(4, 6)}-${expiryCode.slice(6, 8)}`;
    daysRemaining = Math.ceil((expDate.getTime() - now.getTime()) / (1000 * 3600 * 24));
  }

  return {
    valid: true,
    machineId,
    expiryCode,
    expiresAt,
    daysRemaining,
    licenseKey: expectedKey
  };
}

/**
 * Checks current machine license status from data/license.key
 */
function getLicenseStatus() {
  const machineId = getHardwareMachineId();
  try {
    if (!fs.existsSync(LICENSE_FILE)) {
      return {
        licensed: false,
        machineId,
        hostname: os.hostname(),
        reason: 'This laptop is not activated yet. Please enter an Activation Key or Admin Install Password.'
      };
    }
    const raw = JSON.parse(fs.readFileSync(LICENSE_FILE, 'utf8'));
    const check = verifyLicenseKey(raw.licenseKey, machineId);
    if (!check.valid) {
      return {
        licensed: false,
        machineId,
        hostname: os.hostname(),
        reason: check.reason
      };
    }
    return {
      licensed: true,
      machineId,
      hostname: os.hostname(),
      clientName: raw.clientName || 'Authorized Client Laptop',
      activatedAt: raw.activatedAt || '',
      expiresAt: check.expiresAt,
      daysRemaining: check.daysRemaining,
      licenseKeyMasked: `${check.licenseKey.slice(0, 12)}...${check.licenseKey.slice(-6)}`
    };
  } catch (err) {
    return {
      licensed: false,
      machineId,
      hostname: os.hostname(),
      reason: 'Corrupted license file. Please re-activate.'
    };
  }
}

/**
 * Activates the current laptop using either:
 * 1. A valid hardware-locked License Key (from Admin_KeyGenerator.bat)
 * 2. Rupesh's Master Admin Password + desired validity days
 */
function activateCurrentMachine({ licenseKey, adminPassword, validityDays = 'LIFETIME', clientName = 'Authorized Client' }) {
  const machineId = getHardwareMachineId();
  let finalKey = (licenseKey || '').trim().toUpperCase();

  if (adminPassword) {
    const pinHash = crypto
      .createHash('sha256')
      .update(String(adminPassword).trim() + SECRET_SIGNING_SALT)
      .digest('hex');

    if (pinHash !== MASTER_ADMIN_PIN_HASH) {
      throw new Error('Invalid Master Admin Password.');
    }

    let expiryCode = 'LIFETIME';
    if (validityDays && String(validityDays).toUpperCase() !== 'LIFETIME') {
      const days = parseInt(validityDays, 10);
      if (days > 0) {
        const d = new Date(Date.now() + days * 86400000);
        const yyyy = d.getFullYear();
        const mm = String(d.getMonth() + 1).padStart(2, '0');
        const dd = String(d.getDate()).padStart(2, '0');
        expiryCode = `${yyyy}${mm}${dd}`;
      }
    }
    finalKey = generateLicenseKeyForMachine(machineId, expiryCode);
  }

  const verification = verifyLicenseKey(finalKey, machineId);
  if (!verification.valid) {
    throw new Error(verification.reason);
  }

  if (!fs.existsSync(DATA_DIR)) {
    fs.mkdirSync(DATA_DIR, { recursive: true });
  }

  const record = {
    machineId,
    hostname: os.hostname(),
    clientName: (clientName || 'Authorized Client').trim(),
    licenseKey: verification.licenseKey,
    expiresAt: verification.expiresAt,
    activatedAt: new Date().toISOString()
  };

  fs.writeFileSync(LICENSE_FILE, JSON.stringify(record, null, 2), 'utf8');
  return getLicenseStatus();
}

module.exports = {
  getHardwareMachineId,
  generateLicenseKeyForMachine,
  verifyLicenseKey,
  getLicenseStatus,
  activateCurrentMachine
};

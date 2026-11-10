const readline = require('readline');
const {
  getHardwareMachineId,
  generateLicenseKeyForMachine
} = require('./licenseManager');

const args = process.argv.slice(2);

if (args.length >= 1) {
  const machineId = args[0].trim().toUpperCase();
  const daysArg = (args[1] || 'LIFETIME').trim().toUpperCase();
  let expiryCode = 'LIFETIME';
  if (daysArg !== 'LIFETIME' && /^\d+$/.test(daysArg)) {
    const days = parseInt(daysArg, 10);
    const d = new Date(Date.now() + days * 86400000);
    expiryCode = `${d.getFullYear()}${String(d.getMonth() + 1).padStart(2, '0')}${String(d.getDate()).padStart(2, '0')}`;
  } else if (/^\d{8}$/.test(daysArg)) {
    expiryCode = daysArg;
  }
  const key = generateLicenseKeyForMachine(machineId, expiryCode);
  console.log('\n====================================================================');
  console.log('  GENERATED HARDWARE-LOCKED CLIENT LICENSE KEY');
  console.log('====================================================================');
  console.log(`  Target Machine ID : ${machineId}`);
  console.log(`  Expiry            : ${expiryCode}`);
  console.log(`  License Key       : ${key}`);
  console.log('====================================================================\n');
  process.exit(0);
}

const rl = readline.createInterface({
  input: process.stdin,
  output: process.stdout
});

console.log('====================================================================');
console.log('  RUPESH ADMIN LICENSE KEY GENERATOR (HARDWARE-LOCKED)');
console.log('====================================================================');
console.log(`  Your Admin PC Machine ID : ${getHardwareMachineId()}`);
console.log('--------------------------------------------------------------------');

rl.question('\n1. Enter Client Laptop Machine ID (e.g. RUP-XXXX-XXXX-XXXX)\n   [Press Enter to use this laptop]: ', (midAns) => {
  const machineId = (midAns || '').trim().toUpperCase() || getHardwareMachineId();
  rl.question('\n2. Enter Validity in Days (e.g. 365, 90, 30) or press Enter for LIFETIME: ', (valAns) => {
    const rawVal = (valAns || '').trim().toUpperCase() || 'LIFETIME';
    let expiryCode = 'LIFETIME';
    if (rawVal !== 'LIFETIME' && /^\d+$/.test(rawVal)) {
      const days = parseInt(rawVal, 10);
      const d = new Date(Date.now() + days * 86400000);
      expiryCode = `${d.getFullYear()}${String(d.getMonth() + 1).padStart(2, '0')}${String(d.getDate()).padStart(2, '0')}`;
    }

    const licenseKey = generateLicenseKeyForMachine(machineId, expiryCode);
    console.log('\n====================================================================');
    console.log('  SUCCESS! COPY & SEND THIS KEY TO THE CLIENT LAPTOP:');
    console.log('====================================================================');
    console.log(`  Client Machine ID : ${machineId}`);
    console.log(`  License Validity  : ${expiryCode}`);
    console.log(`  ACTIVATION KEY    : ${licenseKey}`);
    console.log('====================================================================\n');
    rl.close();
  });
});

// Get our requirements
const moment = require('moment');
const levels = {
    'trace': 0,
    'debug': 1,
    'info': 2,
    'warn': 3,
    'error': 4
};

// Set logging level (initialise as trace for logging pre-config)
let LOGGINGLEVEL = 'trace';
exports.setLevel = (configLevel) => {
    LOGGINGLEVEL = configLevel;
};

// Master logging function
exports.log = (content, type = 'info') => {
    // Get current date & time
    const TIME = `[${moment().format('YYYY-MM-DD HH:mm:ss')}]`;
    // Output based on log level
    const MSGLEVEL = levels[type.toLowerCase()];
    if (MSGLEVEL >= levels[LOGGINGLEVEL.toLowerCase()]) {
        if (MSGLEVEL >= levels['warn']) {
            console.error(`${TIME} ${type.toUpperCase().padStart(5, ' ')} ${content}`);
        } else {
            console.log(`${TIME} ${type.toUpperCase().padStart(5, ' ')} ${content}`);
        }
    }
    return;
};

// Aliases to log at logging levels
exports.error = (args) => this.log(args, 'error');
exports.warn  = (args) => this.log(args, 'warn');
exports.info  = (args) => this.log(args, 'info');
exports.debug = (args) => this.log(args, 'debug');
exports.trace = (args, obj) => {
    if (obj) args = `${args}\n${JSON.stringify(obj, null, 4)}`;
    this.log(args, 'trace');
};
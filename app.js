const debug = require('debug');
const _ = require('lodash');
const async = require('async');

const doDebug = debug('streamActionsBot');
const functionName = function functionName() {
  doDebug('Hello %o', 'world');
};

functionName();

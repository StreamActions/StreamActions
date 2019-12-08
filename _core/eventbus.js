const { EventEmitter } = require('events');

// TODO: Add other methods needed from the EventEmitter.
//       Also maybe update the method names to something better.

class EventBus extends EventEmitter {
  constructor() {
    super(); // Creates a new instance of the EventEmitter.
    this.setMaxListeners(10); // Set total amount of listeners allowed for a single event.
  }

  // Binds an event to the EventEmitter non-asynchronously.
  bindSync(eventName, callBack) {
    this.addListener(eventName, callBack);
  }

  // Bind an event but at the top of the list non-asynchronously
  bindFirstSync(eventName, callBack) {
    this.prependListener(eventName, callBack);
  }

  // Binds an event to the EventEmitter asynchronously.
  bindAsync(eventName, callBack) {
    this.addListener(eventName, setImmediate(callBack));
  }

  // Binds an event but at the top of to the EventEmitter list asynchronously.
  bindFirstAsync(eventName, callBack) {
    this.prependListener(eventName, setImmediate(callBack));
  }

  // Removes a listener from the EventEmitter.
  unBind(eventName, callBack) {
    this.removeListener(eventName, callBack);
  }
}

module.exports = new EventBus(); // Export a singleton of this class.

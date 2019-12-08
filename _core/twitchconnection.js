const WebSocket = require('ws');

class TwitchConnection extends WebSocket {
  constructor(address) {
    super(address);

    this.onopen = function opened() {
      // nothing yet.
    };

    this.onclose = function closed() {
      // nothing yet.
    };

    this.onerror = function error() {
      // nothing yet.
    };

    this.onmessage = function message() {
      // nothing yet.
    };
  }

  sendRaw(message) {
    // TODO: Add rate limit logic.
    super.send(message);
  }

  send(channel, message) {
    // TODO: Add rate limit logic.
    this.sendRaw(`PRIVMSG #${channel} :${message}`);
  }
}

module.exports = TwitchConnection;

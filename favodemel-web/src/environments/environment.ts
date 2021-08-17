export const environment = {
  production: false,
  apiUrl: 'https://localhost:54300',
  mensageriaConfig: {
    brokerURL: 'ws://host.docker.internal:15674/ws',
    connectHeaders: {
      login: 'guest',
      passcode: 'guest'
    },
    heartbeatIncoming: 0,
    heartbeatOutgoing: 20000,
    reconnectDelay: 200
  },
};

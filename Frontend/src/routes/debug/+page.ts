// A browser-only view of the socket: nothing meaningful renders on the server,
// and the panels initialise from localStorage (backend override, traffic
// filter) which a server render cannot see and would hydrate over.
export const ssr = false;

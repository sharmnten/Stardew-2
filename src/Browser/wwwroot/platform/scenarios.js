// Imported only by a host compiled with BrowserPortTesting=true.
export function start(reference) {
  window.portScenarios = {
    load: id => reference.invokeMethodAsync('LoadScenario', id),
    run: async id => JSON.parse(await reference.invokeMethodAsync('RunScenarioAction', id)),
    snapshot: async () => JSON.parse(await reference.invokeMethodAsync('ScenarioSnapshot'))
  };
}

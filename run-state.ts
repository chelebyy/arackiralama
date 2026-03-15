import { deriveState } from "file:///C:/Users/muham/AppData/Roaming/npm/node_modules/gsd-pi/src/resources/extensions/gsd/state.ts";

async function run() {
  const result = await deriveState("C:/All_Project/Arac-Kiralama");
  console.log(JSON.stringify(result, null, 2));
}

run().catch(console.error);
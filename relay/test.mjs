// Behaviour test for the relay. Usage: node test.mjs ws://127.0.0.1:8787   (Node 22+)
const base = process.argv[2] || "ws://127.0.0.1:8787";
const code = String(Math.floor(1000 + Math.random() * 9000));
let failed = 0;
const check = (name, ok, extra = "") => { console.log((ok ? "PASS " : "FAIL ") + name + (extra ? "  " + extra : "")); if (!ok) failed++; };

function open(role, key) {
  return new Promise((resolve) => {
    const ws = new WebSocket(`${base}/room/${code}?role=${role}&key=${key}`);
    const c = { ws, msgs: [], closed: null };
    ws.onmessage = (e) => c.msgs.push(String(e.data));
    ws.onclose = (e) => { c.closed = e.code; };
    ws.onopen = () => resolve(c);
    ws.onerror = () => resolve(c);
    setTimeout(() => resolve(c), 4000);
  });
}
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// 1. guest before any host -> not found
let g0 = await open("guest", "g1"); await sleep(400);
check("guest without a host is refused (4004)", g0.closed === 4004, "closed=" + g0.closed);

// 2. host registers
let h = await open("host", "h1"); await sleep(300);
check("host gets R", h.msgs.includes("R"), JSON.stringify(h.msgs));

// 3. another host with another key is refused
let h2 = await open("host", "other"); await sleep(400);
check("second host with a different key is refused (4001)", h2.closed === 4001, "closed=" + h2.closed);
check("original host still connected", h.closed === null);

// 4. guest joins
let g = await open("guest", "g1"); await sleep(300);
check("guest gets A", g.msgs.includes("A"), JSON.stringify(g.msgs));
check("host gets J", h.msgs.includes("J"), JSON.stringify(h.msgs));

// 5. messages flow both ways
h.ws.send("S|123"); g.ws.send("H|Doha|2"); await sleep(300);
check("host -> guest relayed", g.msgs.includes("S|123"));
check("guest -> host relayed", h.msgs.includes("H|Doha|2"));
g.ws.send("p"); await sleep(150);
check("keep-alive 'p' is not forwarded", !h.msgs.includes("p"));

// 6. a third player is refused
let g2 = await open("guest", "intruder"); await sleep(400);
check("second guest with another key is refused (4003)", g2.closed === 4003, "closed=" + g2.closed);

// 7. host reconnects with the same key (e.g. phone came back from WhatsApp)
const gBefore = g.msgs.length;
let hNew = await open("host", "h1"); await sleep(500);
check("reconnecting host gets R and J", hNew.msgs.includes("R") && hNew.msgs.includes("J"), JSON.stringify(hNew.msgs));
check("old host socket was replaced (4002)", h.closed === 4002, "closed=" + h.closed);
check("guest is NOT told the host left", !g.msgs.slice(gBefore).includes("L"), JSON.stringify(g.msgs.slice(gBefore)));
hNew.ws.send("S|456"); await sleep(250);
check("relay works after the host reconnected", g.msgs.includes("S|456"));

// 8. guest reconnects with the same key
let gNew = await open("guest", "g1"); await sleep(500);
check("reconnecting guest gets A", gNew.msgs.includes("A"), JSON.stringify(gNew.msgs));
check("host is NOT told the guest left", !hNew.msgs.includes("L"), JSON.stringify(hNew.msgs));

// 9. guest leaves -> host gets L
gNew.ws.close(1000); await sleep(500);
check("host gets L when the guest leaves", hNew.msgs.includes("L"), JSON.stringify(hNew.msgs));

// 10. host leaves -> a joining guest finds nobody after a moment
hNew.ws.close(1000); await sleep(500);
let g3 = await open("guest", "g9"); await sleep(400);
check("after the host left the room is gone (4004)", g3.closed === 4004, "closed=" + g3.closed);

console.log(failed === 0 ? "\nALL PASSED" : `\n${failed} FAILED`);
process.exit(failed === 0 ? 0 : 1);

// Contrato do Local BotServer contra o .exe compilado. Roda uma copia em pasta temporaria, em porta
// propria, sem tocar no config.env nem no servidor aberto do usuario. A janela abre durante o teste.
// Uso: node tests/server_contracts.js   (Node 22+)
const assert = require('assert');
const fs = require('fs');
const net = require('net');
const os = require('os');
const path = require('path');
const { spawn } = require('child_process');

const PORT = 18765;
const URL = `ws://127.0.0.1:${PORT}/`;
const build = path.join(__dirname, '..', 'bin');
const exe = path.join(build, 'LocalBotServer.exe');
assert(fs.existsSync(exe), 'compile LocalBotServer.exe antes (build.bat)');

const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'local-botserver-'));
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
const readLog = () => fs.readFileSync(path.join(dir, 'logs', 'output.log'), 'utf8');

async function until(check, what, ms = 8000) {
  const limit = Date.now() + ms;
  while (Date.now() < limit) {
    if (await check()) return;
    await sleep(50);
  }
  throw new Error('tempo esgotado: ' + what);
}

function reachable(host) {
  return new Promise((resolve) => {
    const socket = net.connect({ host, port: PORT });
    socket.setTimeout(1500, () => { socket.destroy(); resolve(false); });
    socket.on('connect', () => { socket.destroy(); resolve(true); });
    socket.on('error', () => resolve(false));
  });
}

function client(name, channel, { echoSeq = false } = {}) {
  return new Promise((resolve, reject) => {
    const ws = new WebSocket(URL);
    const state = { ws, messages: [], pings: [], closed: false };
    state.send = (data) => ws.send(typeof data === 'string' ? data : JSON.stringify(data));
    ws.onmessage = (event) => {
      const data = JSON.parse(event.data);
      if (data.type !== 'ping') return state.messages.push(data);
      state.pings.push(data);
      if (echoSeq) state.send({ type: 'ping', seq: data.seq });
    };
    ws.onclose = () => { state.closed = true; };
    ws.onerror = () => reject(new Error('conexao recusada'));
    ws.onopen = () => {
      if (name) state.send({ type: 'init', name, channel, rooms: true });
      resolve(state);
    };
  });
}

async function main() {
  // O .config viaja junto: sem ele o Windows 8+ pede para instalar o .NET 3.5.
  for (const name of ['LocalBotServer.exe', 'LocalBotServer.exe.config']) {
    fs.copyFileSync(path.join(build, name), path.join(dir, name));
  }
  fs.writeFileSync(path.join(dir, 'config.env'), `BOTSERVER_WS_PORT=${PORT}\nBOTSERVER_PING_INTERVAL_MS=200\n`);
  const child = spawn(path.join(dir, 'LocalBotServer.exe'), { stdio: 'ignore' });
  try {
    await until(() => reachable('127.0.0.1'), 'servidor abrir a porta', 20000);

    const lan = Object.values(os.networkInterfaces()).flat().find((i) => i.family === 'IPv4' && !i.internal);
    if (lan) assert.strictEqual(await reachable(lan.address), false, 'sem BOTSERVER_LAN so o loopback responde');

    const page = await fetch(`http://127.0.0.1:${PORT}/`);
    assert.strictEqual(page.status, 200);
    assert((await page.text()).includes('BotServer Online'), 'pagina de teste');

    const readStatus = async () => (await fetch(`http://127.0.0.1:${PORT}/status`)).json();
    assert.deepStrictEqual(await readStatus(), { server: 'dibrabot-local', online: 0, rooms: 0 }, 'status sem ninguem');

    const a = await client('Alfa', 'guild war', { echoSeq: true });
    const b = await client('Beta', 'guild war', { echoSeq: true });
    const c = await client('Gama', 'hunt');
    await until(() => readLog().includes('Gama joined'), 'init dos tres');
    assert.deepStrictEqual(await readStatus(), { server: 'dibrabot-local', online: 3, rooms: 2 }, 'status com players');
    assert.strictEqual(a.messages.length, 0, 'init nao e respondido: o DibraBot precisa decidir pelo modo comum');

    a.send({ type: 'message', topic: 't', message: { x: 1, l: [true, 'ação'], n: null } });
    await until(() => a.messages.length && b.messages.length, 'mensagem da sala');
    assert.deepStrictEqual(b.messages[0], { type: 'message', id: 1, name: 'Alfa', topic: 't', message: { x: 1, l: [true, 'ação'], n: null } });
    assert.strictEqual(c.messages.length, 0, 'outra sala nao recebe');

    const big = 'z'.repeat(60000);
    a.send({ type: 'message', topic: 'big', message: big });
    await until(() => b.messages.some((m) => m.topic === 'big' && m.message === big), 'quadro de 16 bits');

    b.send({ type: 'message', topic: 'list' });
    await until(() => b.messages.some((m) => m.topic === 'list'), 'lista');
    assert.deepStrictEqual(b.messages.find((m) => m.topic === 'list').message.sort(), ['Alfa', 'Beta']);

    // Pings proprios do bot (sem seq) nao podem virar latencia de quem devolve o seq.
    const noise = setInterval(() => a.send({ type: 'ping' }), 50);
    await sleep(1500);
    clearInterval(noise);
    assert(a.pings.length >= 4 && a.pings.every((p) => Number.isInteger(p.seq)), 'ping numerado');
    assert(a.pings.slice(-1)[0].seq > a.pings[0].seq, 'seq avanca quando respondido');
    assert(a.pings.every((p) => p.ping < 150), 'latencia local pequena: ' + JSON.stringify(a.pings));

    for (const [text, label] of [['{x', 'invalid JSON'], [JSON.stringify({ type: 'message', topic: 't' }), 'invalid init']]) {
      const bad = await client();
      bad.send(text);
      await until(() => bad.closed, label);
      await until(() => readLog().includes(label), 'log ' + label);
    }
    const huge = await client('Grande', 'hunt');
    huge.send('x'.repeat(70000));
    await until(() => huge.closed, 'pacote acima do limite');
    const flood = await client('Flood', 'hunt');
    for (let i = 0; i < 150; i++) flood.send({ type: 'message', topic: 'f', message: i });
    await until(() => flood.closed, 'limite de pacotes');
    const forged = await client('X\r\n[01.01 00:00:00] OK forjada', 'hunt');
    await until(() => readLog().includes('forjada joined'), 'nome com quebra de linha');
    assert(!/^\[01\.01 00:00:00\]/m.test(readLog()), 'nome nao forja linha no log');

    const again = spawn(path.join(dir, 'LocalBotServer.exe'), { stdio: 'ignore' });
    const code = await new Promise((resolve) => again.on('exit', resolve));
    assert.strictEqual(code, 0, 'segunda instancia so chama a primeira');
    assert.strictEqual(child.exitCode, null, 'a primeira continua');

    forged.ws.close();
    c.ws.close();
    await until(() => readLog().includes('Room "hunt" closed'), 'sala vazia fecha');
    a.ws.close();
    b.ws.close();
    await until(() => readLog().includes('Room "guild war" closed'), 'ultima sala fecha');
    assert(!/ ERROR /.test(readLog()), 'sem erro no log:\n' + readLog());
    console.log('server_contracts: OK');
  } finally {
    child.kill();
    await sleep(500);
    fs.rmSync(dir, { recursive: true, force: true });
  }
}

main().catch((error) => { console.error(error); process.exit(1); });

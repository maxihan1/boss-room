// 게임 API 앞에 서서 /api/login 응답만 DELAY_MS 만큼 늦춰 넘겨주는 시험용 통로
import http from 'node:http';
const 대상 = process.env.TARGET.replace(/\/$/, ''), 늦춤 = Number(process.env.DELAY_MS ?? 4000);
http.createServer(async (req, res) => {
  const 몸 = []; for await (const c of req) 몸.push(c);
  const h = { ...req.headers }; delete h.host; delete h['content-length'];
  const r = await fetch(대상 + req.url, { method: req.method, headers: h, body: ['GET', 'HEAD'].includes(req.method) ? undefined : Buffer.concat(몸) });
  const 답 = Buffer.from(await r.arrayBuffer());
  if (req.url.includes('/login')) await new Promise((f) => setTimeout(f, 늦춤));
  res.writeHead(r.status, { 'content-type': r.headers.get('content-type') ?? 'application/json' }); res.end(답);
}).listen(7799, '127.0.0.1');

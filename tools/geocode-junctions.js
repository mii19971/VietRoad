// Tim toa do NUT GIAO tu ten 2 pho (vd "Giảng Võ - Cát Linh") bang Overpass (OpenStreetMap),
// xuat file CSV dung mau nhap hang loat cua trang Admin (/alerts/import) de NGUOI duyet truoc khi nhap.
//
//   node tools/geocode-junctions.js <input.txt> <output.csv> [bbox]
//
// input.txt: moi dong 1 diem, dang "Pho A - Pho B" (dong bat dau bang # bi bo qua).
// bbox mac dinh: Ha Noi noi thanh "20.90,105.70,21.15,105.95" (nam,tay,bac,dong).
// Dong khong tim duoc nut giao -> ghi vao file *.chua-tim-duoc.txt de nhap tay.

const fs = require('fs');

const [input, output, bbox = '20.90,105.70,21.15,105.95'] = process.argv.slice(2);
if (!input || !output) {
  console.error('Cach dung: node tools/geocode-junctions.js <input.txt> <output.csv> [bbox]');
  process.exit(1);
}

const ENDPOINT = 'https://overpass-api.de/api/interpreter';
const USER_AGENT = 'VietRoad-Importer/0.1';
const MAX_SPREAD_M = 200; // cac diem giao (duong doi 2 chieu...) cach nhau qua xa -> khong chac, bo

const escapeRegex = s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
const sleep = ms => new Promise(r => setTimeout(r, ms));

function distanceM(a, b) {
  const dy = (a.lat - b.lat) * 110574;
  const dx = (a.lon - b.lon) * 111320 * Math.cos(a.lat * Math.PI / 180);
  return Math.hypot(dx, dy);
}

// Node chung cua 2 nhom duong: ten ket thuc bang "Giảng Võ" (khop "Phố Giảng Võ", "Đường Giảng Võ").
async function junction(a, b) {
  const q = `[out:json][timeout:60];
    way["highway"]["name"~"(^| )${escapeRegex(a)}$",i](${bbox})->.a;
    way["highway"]["name"~"(^| )${escapeRegex(b)}$",i](${bbox})->.b;
    node(w.a)(w.b);
    out;`;
  for (let attempt = 1; attempt <= 3; attempt++) {
    let res;
    try {
      res = await fetch(ENDPOINT, {
        method: 'POST',
        headers: { 'User-Agent': USER_AGENT, 'Content-Type': 'application/x-www-form-urlencoded' },
        body: 'data=' + encodeURIComponent(q),
        signal: AbortSignal.timeout(90000),
      });
    } catch (e) {
      // Loi mang / het gio: doi roi thu lai.
      if (attempt === 3) throw e;
      await sleep(20000 * attempt);
      continue;
    }
    if (res.ok) return (await res.json()).elements.map(e => ({ lat: e.lat, lon: e.lon }));
    if (![429, 502, 503, 504].includes(res.status)) throw new Error(`Overpass ${res.status}`);
    await sleep(20000 * attempt);
  }
  throw new Error('Overpass ban, thu lai sau');
}

const csvCell = s => (/[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s);

(async () => {
  const lines = fs.readFileSync(input, 'utf8').split(/\r?\n/).map(l => l.trim()).filter(l => l && !l.startsWith('#'));
  const rows = ['STT,Loại,Tên,Tọa độ,Giới hạn tốc độ,Hướng,Ghi chú'];
  const failed = [];

  for (const line of lines) {
    const parts = line.split(/\s+-\s+/);
    if (parts.length < 2) { failed.push(`${line}  (khong phai nut giao 2 pho)`); continue; }

    // Nut giao 3 pho ("A - B - C"): lay giao cua 2 pho dau.
    const points = await junction(parts[0], parts[1]);
    await sleep(2000); // lich su voi may chu cong cong

    if (points.length === 0) { failed.push(`${line}  (khong thay 2 pho giao nhau tren OSM)`); continue; }
    const center = {
      lat: points.reduce((s, p) => s + p.lat, 0) / points.length,
      lon: points.reduce((s, p) => s + p.lon, 0) / points.length,
    };
    const spread = Math.max(...points.map(p => distanceM(p, center)));
    if (spread > MAX_SPREAD_M) { failed.push(`${line}  (${points.length} diem giao cach nhau ${Math.round(spread)} m - khong chac)`); continue; }

    rows.push([
      rows.length, 'Camera giám sát', `Nút giao ${line}`,
      `${center.lat.toFixed(6)}, ${center.lon.toFixed(6)}`, '', '',
      `Tâm nút giao (${points.length} điểm OSM). Nguồn: UBND TP Hà Nội, 49 điểm camera, 03/2025`,
    ].map(String).map(csvCell).join(','));
    console.log(`OK   ${line}  -> ${center.lat.toFixed(5)}, ${center.lon.toFixed(5)}`);
  }

  // BOM: Excel mo dung tieng Viet.
  fs.writeFileSync(output, '﻿' + rows.join('\r\n') + '\r\n');
  const failedFile = output.replace(/\.csv$/i, '') + '.chua-tim-duoc.txt';
  fs.writeFileSync(failedFile, failed.join('\r\n') + '\r\n');
  console.log(`\nTim duoc ${rows.length - 1}, chua tim duoc ${failed.length} (xem ${failedFile}).`);
})().catch(e => { console.error(e.message); process.exit(1); });

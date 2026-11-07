// Sinh tệp dữ liệu MẪU đóng gói trong app: app/src/main/assets/vietroad_data.json
// Chạy: node tools/gen-sample-data.mjs
//
// CẢNH BÁO: toạ độ tuyến đường chỉ là gần đúng, dùng để chạy thử / mô phỏng.
// Trước khi phát hành phải thay bằng dữ liệu khảo sát thực tế (cùng định dạng JSON).

import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

// Giống java.util.UUID.nameUUIDFromBytes (MD5, phiên bản 3) — phải khớp DataFields.featureUid/zoneUid trong app
function nameUuid(key) {
  const b = crypto.createHash('md5').update(key, 'utf8').digest();
  b[6] = (b[6] & 0x0f) | 0x30;
  b[8] = (b[8] & 0x3f) | 0x80;
  const h = b.toString('hex');
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-${h.slice(12, 16)}-${h.slice(16, 20)}-${h.slice(20)}`;
}
const featureUid = (code, lat, lng, heading) =>
  nameUuid(`f|${code}|${lat.toFixed(6)}|${lng.toFixed(6)}|${heading == null ? '' : Math.round(heading)}`);
const zoneUid = (name, pts) => {
  const [a, b] = [pts[0], pts[pts.length - 1]];
  return nameUuid(`z|${name ?? ''}|${a[0].toFixed(6)}|${a[1].toFixed(6)}|${b[0].toFixed(6)}|${b[1].toFixed(6)}`);
};

// Dữ liệu mẫu: chưa xác minh, độ tin cậy thấp
const SAMPLE_META = { data_source: 'sample', confidence: 0.2, verified_at: null, expires_at: null, status: 'active' };

const toRad = (d) => (d * Math.PI) / 180;
function distance(lat1, lng1, lat2, lng2) {
  const dLat = toRad(lat2 - lat1), dLng = toRad(lng2 - lng1);
  const a = Math.sin(dLat / 2) ** 2 + Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) * Math.sin(dLng / 2) ** 2;
  return 2 * 6371000 * Math.asin(Math.min(1, Math.sqrt(a)));
}
function bearing(lat1, lng1, lat2, lng2) {
  const y = Math.sin(toRad(lng2 - lng1)) * Math.cos(toRad(lat2));
  const x = Math.cos(toRad(lat1)) * Math.sin(toRad(lat2)) - Math.sin(toRad(lat1)) * Math.cos(toRad(lat2)) * Math.cos(toRad(lng2 - lng1));
  return ((Math.atan2(y, x) * 180) / Math.PI + 360) % 360;
}
function pointAlong(points, frac) {
  const lens = points.slice(1).map((p, i) => distance(points[i][0], points[i][1], p[0], p[1]));
  let target = frac * lens.reduce((a, b) => a + b, 0);
  for (let i = 0; i < lens.length; i++) {
    if (target <= lens[i] || i === lens.length - 1) {
      const t = lens[i] ? Math.min(1, target / lens[i]) : 0;
      const [a, b] = [points[i], points[i + 1]];
      return { lat: a[0] + (b[0] - a[0]) * t, lng: a[1] + (b[1] - a[1]) * t, bearing: bearing(a[0], a[1], b[0], b[1]) };
    }
    target -= lens[i];
  }
}

// [mã, vị trí dọc tuyến 0..1, chiều: fwd = theo chiều vẽ, rev = ngược lại, both = mọi hướng]
const ROUTES = [
  {
    name: 'Đại lộ Thăng Long', province: 'Hà Nội', car: 100, truck: 90,
    points: [[21.0136, 105.527], [21.0125, 105.56], [21.01, 105.6], [21.007, 105.64], [21.004, 105.68],
      [21.001, 105.72], [21.0, 105.75], [21.0045, 105.775], [21.0085, 105.795]],
    features: [
      ['IE.452', 0.01, 'fwd'], ['P.127', 0.02, 'fwd'], ['P.127', 0.98, 'rev'],
      ['CAM_SPEED', 0.3, 'fwd'], ['CAM_SPEED', 0.55, 'rev'], ['CAM_SPEED', 0.75, 'fwd'],
      ['BLACKSPOT', 0.45, 'both'], ['W.201b', 0.88, 'fwd'], ['CAM_MOBILE', 0.65, 'rev'],
      ['W.227', 0.18, 'fwd'], ['TOLL', 0.93, 'both'],
    ],
  },
  {
    name: 'Cao tốc TP.HCM – Long Thành – Dầu Giây', province: 'TP. Hồ Chí Minh', car: 100, truck: 90,
    points: [[10.7905, 106.774], [10.788, 106.81], [10.779, 106.85], [10.77, 106.89], [10.765, 106.93],
      [10.78, 106.99], [10.82, 107.05], [10.86, 107.12], [10.93, 107.2]],
    features: [
      ['IE.452', 0.01, 'fwd'], ['P.127', 0.02, 'fwd'], ['P.127', 0.98, 'rev'],
      ['CAM_SPEED', 0.2, 'fwd'], ['CAM_SPEED', 0.5, 'rev'], ['CAM_SPEED', 0.8, 'fwd'],
      ['TOLL', 0.4, 'both'], ['REST', 0.6, 'fwd'], ['IE.453', 0.97, 'fwd'], ['W.227', 0.7, 'rev'],
    ],
  },
  {
    name: 'Đường Láng', province: 'Hà Nội', car: 50, truck: 50,
    points: [[21.0255, 105.803], [21.0185, 105.807], [21.011, 105.81], [21.0035, 105.814]],
    features: [
      ['P.127', 0.05, 'fwd'], ['W.225', 0.3, 'fwd'], ['CAM_REDLIGHT', 0.5, 'fwd'],
      ['CAM_REDLIGHT', 0.52, 'rev'], ['P.123a', 0.7, 'fwd'], ['P.131a', 0.85, 'both'], ['P.106a', 0.1, 'both'],
    ],
  },
  {
    name: 'Đường Nguyễn Tất Thành', province: 'Đà Nẵng', car: 60, truck: 50,
    points: [[16.079, 108.22], [16.074, 108.205], [16.07, 108.195], [16.06, 108.175]],
    features: [['P.127', 0.05, 'fwd'], ['CAM_TRAFFIC', 0.5, 'both'], ['FLOOD', 0.7, 'both'], ['W.224', 0.3, 'fwd']],
  },
];

// Điểm lẻ trong nội đô: [mã, lat, lng, hướng (null = mọi hướng), đường, tỉnh]
const SINGLES = [
  ['CAM_REDLIGHT', 21.0285, 105.852, null, 'Đinh Tiên Hoàng', 'Hà Nội'],
  ['P.102', 21.03, 105.848, 90, 'Hàng Bông', 'Hà Nội'],
  ['P.124a', 21.0175, 105.8425, null, 'Tôn Đức Thắng', 'Hà Nội'],
  ['W.210', 21.0153, 105.8414, 0, 'Lê Duẩn', 'Hà Nội'],
  ['CAM_REDLIGHT', 10.7769, 106.7009, null, 'Lê Duẩn – Pasteur', 'TP. Hồ Chí Minh'],
  ['P.123b', 10.7727, 106.6981, 180, 'Nguyễn Thị Minh Khai', 'TP. Hồ Chí Minh'],
  ['BLACKSPOT', 10.8491, 106.7724, null, 'Ngã tư Thủ Đức', 'TP. Hồ Chí Minh'],
  ['FLOOD', 10.8012, 106.7135, null, 'Nguyễn Hữu Cảnh', 'TP. Hồ Chí Minh'],
  ['P.117', 10.7905, 106.7128, null, 'Cầu Sài Gòn', 'TP. Hồ Chí Minh'],
];

const features = [];
const zones = [];
for (const r of ROUTES) {
  zones.push({
    uid: zoneUid(r.name, r.points),
    name: r.name, points: r.points, max_speed_car: r.car, max_speed_truck: r.truck, oneway: false, ...SAMPLE_META,
  });
  for (const [code, frac, dir] of r.features) {
    const p = pointAlong(r.points, frac);
    const lat = +p.lat.toFixed(6);
    const lng = +p.lng.toFixed(6);
    const heading = dir === 'both' ? null : Math.round(dir === 'fwd' ? p.bearing : (p.bearing + 180) % 360);
    features.push({
      uid: featureUid(code, lat, lng, heading),
      type_code: code,
      lat,
      lng,
      heading,
      speed_limit: code === 'P.127' ? r.car : null,
      vehicles: code === 'P.106a' ? 'truck' : 'all',
      road_name: r.name,
      province: r.province,
      note: 'Dữ liệu mẫu',
      ...SAMPLE_META,
    });
  }
}
for (const [code, lat, lng, heading, road, province] of SINGLES) {
  features.push({
    uid: featureUid(code, lat, lng, heading),
    type_code: code, lat, lng, heading, speed_limit: null, vehicles: 'all', road_name: road, province, note: 'Dữ liệu mẫu',
    ...SAMPLE_META,
  });
}

const out = {
  format: 'vietroad',
  note: 'DỮ LIỆU MẪU — toạ độ gần đúng, chỉ dùng chạy thử. Thay bằng dữ liệu khảo sát thực tế trước khi phát hành.',
  features,
  zones,
};
const here = path.dirname(fileURLToPath(import.meta.url));
const file = path.join(here, '..', 'app', 'src', 'main', 'assets', 'vietroad_data.json');
fs.writeFileSync(file, JSON.stringify(out, null, 1));
console.log(`Đã ghi ${features.length} điểm, ${zones.length} đoạn tốc độ → ${file}`);

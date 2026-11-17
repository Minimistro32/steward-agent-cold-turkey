// npm install --prefix /tmp/steward-svg-tools @resvg/resvg-js
// NODE_PATH=/tmp/steward-svg-tools/node_modules node scripts/build_icon.cjs
const fs = require('node:fs');
const path = require('node:path');
const { Resvg } = require('@resvg/resvg-js');
const root = path.resolve(__dirname, '..', 'packaging');
const svg = fs.readFileSync(path.join(root, 'steward-logo.svg'));
const images = new Map();
function png(size) {
  if (!images.has(size)) images.set(size, new Resvg(svg, {fitTo:{mode:'width',value:size}}).render().asPng());
  return images.get(size);
}
const chunks = [['icp4',16],['icp5',32],['icp6',64],['ic07',128],['ic08',256],['ic09',512],['ic10',1024],['ic11',32],['ic12',64],['ic13',256],['ic14',512]].map(([type,size]) => {
 const bytes=png(size);const head=Buffer.alloc(8);head.write(type);head.writeUInt32BE(bytes.length+8,4);return Buffer.concat([head,bytes]);
});
const head=Buffer.alloc(8);head.write('icns');head.writeUInt32BE(8+chunks.reduce((n,b)=>n+b.length,0),4);
fs.writeFileSync(path.join(root,'Steward.icns'),Buffer.concat([head,...chunks]));
fs.writeFileSync(path.join(root,'steward-icon-preview.png'),png(512));

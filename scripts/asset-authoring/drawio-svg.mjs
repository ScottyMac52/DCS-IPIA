// Adapted from DCS-Common f1c968e606414225db837e75426c0c930cc815c9 (MIT).
import { decodeDrawioGraph } from './drawio-source.mjs';

const decode = (value = '') => value
  .replaceAll('&quot;', '"')
  .replaceAll('&lt;', '<')
  .replaceAll('&gt;', '>')
  .replaceAll('&amp;', '&');
const esc = (value = '') => value
  .replaceAll('&', '&amp;')
  .replaceAll('"', '&quot;')
  .replaceAll('<', '&lt;')
  .replaceAll('>', '&gt;');
const attr = (source, name) => decode(source.match(new RegExp(`${name}=\"([^\"]*)\"`))?.[1]);
const styleValue = (style, name) => {
  if (name === 'image') {
    const image = style.match(/(?:^|;)image=(data:image\/[^,;]+,[A-Za-z0-9+/=]+)(?:;|$)/)?.[1];
    return image?.replace(',', ';base64,');
  }
  return style.split(';').find((part) => part.startsWith(`${name}=`))?.slice(name.length + 1);
};

function cells(xml) {
  // Draw.io stores metadata such as tooltips on a UserObject wrapper and may move
  // the cell ID and label there. Normalize those attributes onto the nested
  // mxCell for export without rewriting the authoritative Draw.io source.
  const normalized = xml.replace(
    /<UserObject\b([^>]*)>\s*<mxCell\b([^>]*)>/g,
    (match, userTag, cellTag) => {
      const inherited = [];
      const userId = attr(userTag, 'id');
      const userLabel = attr(userTag, 'label');
      if (userId && !attr(cellTag, 'id')) inherited.push(`id="${esc(userId)}"`);
      if (userLabel && !attr(cellTag, 'value')) inherited.push(`value="${esc(userLabel)}"`);
      return inherited.length
        ? match.replace('<mxCell', `<mxCell ${inherited.join(' ')}`)
        : match;
    },
  );
  return [...normalized.matchAll(/<mxCell\b([^>]*?)(?:\/>|>([\s\S]*?)<\/mxCell>)/g)].map((match) => {
    const tag = match[1];
    const body = match[2] ?? '';
    const geometry = body.match(/<mxGeometry\b([^>]*?)(?:\/>|>([\s\S]*?)<\/mxGeometry>)/);
    const geometryTag = geometry?.[1] ?? '';
    const points = [...(geometry?.[2] ?? '').matchAll(/<mxPoint\b([^>]*)\/>/g)].map((point) => ({
      x: Number(attr(point[1], 'x') || 0),
      y: Number(attr(point[1], 'y') || 0),
    }));
    return {
      id: attr(tag, 'id'), value: attr(tag, 'value'), style: attr(tag, 'style'),
      source: attr(tag, 'source'), target: attr(tag, 'target'),
      vertex: attr(tag, 'vertex') === '1', edge: attr(tag, 'edge') === '1',
      x: Number(attr(geometryTag, 'x') || 0), y: Number(attr(geometryTag, 'y') || 0),
      width: Number(attr(geometryTag, 'width') || 0), height: Number(attr(geometryTag, 'height') || 0),
      points,
    };
  });
}

function endpoint(label, anchor) {
  const ax = anchor.x + anchor.width / 2;
  const ay = anchor.y + anchor.height / 2;
  const cx = label.x + label.width / 2;
  const cy = label.y + label.height / 2;
  const dx = ax - cx;
  const dy = ay - cy;
  if (Math.abs(dx / label.width) > Math.abs(dy / label.height)) {
    return { x: dx > 0 ? label.x + label.width : label.x, y: cy };
  }
  return { x: cx, y: dy > 0 ? label.y + label.height : label.y };
}

function renderLabelBox(lines, label, id, textPrefix = 'lbl', includeButtonNumberWatermarks = false) {
  const style = label.style || '';
  const fillColor = styleValue(style, 'fillColor') || '#1e293b';
  const fontColor = styleValue(style, 'fontColor') || '#f1f5f9';
  const strokeColor = styleValue(style, 'strokeColor') || '#00bfff';
  const fontSize = styleValue(style, 'fontSize') || '11';
  const isTransparent = !fillColor || fillColor === 'none' || fillColor === 'transparent';

  if (!isTransparent) {
    lines.push(`  <rect x=\"${label.x}\" y=\"${label.y}\" width=\"${label.width}\" height=\"${label.height}\" rx=\"4\" fill=\"${esc(fillColor)}\" stroke=\"${esc(strokeColor)}\" stroke-width=\"1.5\"/>`);
  }

  // Vertical centering approximation for pure-text overlays and chips
  const textY = label.y + Math.round(Number(label.height) / 2) + 4;
  lines.push(`  <text id=\"${textPrefix}-${esc(id)}\" x=\"${label.x + label.width / 2}\" y=\"${textY}\" text-anchor=\"middle\" font-family=\"Arial,sans-serif\" font-size=\"${esc(fontSize)}\" fill=\"${esc(fontColor)}\">${esc(label.value)}</text>`);

  // Optional button-number watermark (controlled by export-config.json or --watermarks).
  // Data lives in the draw.io label style as buttonNumber=N.
  if (includeButtonNumberWatermarks) {
    const buttonNumber = styleValue(label.style || '', 'buttonNumber');
    if (buttonNumber) {
      const wx = label.x + label.width - 4;
      const wy = label.y + label.height - 3;
      lines.push(`  <text class=\"btn-watermark\" data-button=\"${esc(buttonNumber)}\" x=\"${wx}\" y=\"${wy}\" text-anchor=\"end\" font-family=\"Arial,sans-serif\" font-size=\"8\" fill=\"#94a3b8\" opacity=\"0.35\">${esc(buttonNumber)}</text>`);
    }
  }
}

export function renderDrawioSvg(device, xml, { watermarks = false } = {}) {
  const graph = decodeDrawioGraph(xml, device.id);
  const modelTag = graph.match(/<mxGraphModel\b([^>]*)>/)?.[1];
  const width = Number(attr(modelTag, 'pageWidth'));
  const height = Number(attr(modelTag, 'pageHeight'));
  if (![width, height].every(value => Number.isFinite(value) && value > 0)) {
    throw new Error(`${device.id}: invalid canvas geometry`);
  }
  const all = cells(graph);
  if (new Set(all.map(cell => cell.id)).size !== all.length) {
    throw new Error(`${device.id}: duplicate draw.io cell IDs`);
  }
  for (const cell of all.filter(cell => cell.vertex && /^(canvas$|footer$|hardware-image-|anchor-|label-|presentation-|mask-)/.test(cell.id))) {
    if (![cell.x, cell.y, cell.width, cell.height].every(Number.isFinite) || cell.width <= 0 || cell.height <= 0) {
      throw new Error(`${device.id}: invalid geometry for ${cell.id}`);
    }
  }
  for (const edge of all.filter(cell => cell.edge)) {
    if (!edge.id.startsWith('connector-'))
      throw new Error(`${device.id}: callout edge must use a stable connector-* ID: ${edge.id || '(missing)'}`);
    const controlId = edge.id.slice('connector-'.length);
    if (edge.source !== `label-${controlId}` || edge.target !== `anchor-${controlId}`)
      throw new Error(`${device.id}: ${edge.id} must connect label-${controlId} to anchor-${controlId}`);
    if (edge.points.some(point => !Number.isFinite(point.x) || !Number.isFinite(point.y)))
      throw new Error(`${device.id}: invalid connector geometry for ${edge.id}`);
  }
  const byId = new Map(all.map((cell) => [cell.id, cell]));
  const canvas = byId.get('canvas');
  const images = all.filter((cell) => cell.id.startsWith('hardware-image-'));
  const connectors = all.filter((cell) => cell.id.startsWith('connector-'));
  const labels = all.filter((cell) => cell.id.startsWith('label-'));
  const presentations = all.filter((cell) => cell.id.startsWith('presentation-'));
  const masks = all.filter((cell) => cell.id.startsWith('mask-'));
  const footer = byId.get('footer');
  // Callout style: connectors + anchors. Box-only style: label-* without connectors
  // (embedded artwork already shows controls; overlay boxes are the binding names).
  if (!canvas || !images.length || !footer) {
    throw new Error(`${device.id}: incomplete native draw.io graph (need canvas, hardware-image-*, footer)`);
  }
  if (!connectors.length && !labels.length) {
    throw new Error(`${device.id}: incomplete native draw.io graph (need connector-* and/or label-*)`);
  }

  const lines = [
    `<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"${width}\" height=\"${height}\" viewBox=\"0 0 ${width} ${height}\">`,
    `  <rect width=\"${width}\" height=\"${height}\" fill=\"${styleValue(canvas.style, 'fillColor')}\"/>`,
  ];
  for (const image of images) {
    const data = styleValue(image.style, 'image');
    const match = data?.match(/^data:image\/(png|jpeg|jpg);base64,([A-Za-z0-9+/=]+)$/);
    if (!match) throw new Error(`${device.id}: ${image.id} requires an embedded raster image`);
    const bytes = Buffer.from(match[2], 'base64');
    const canonical = bytes.toString('base64') === match[2];
    const complete = match[1] === 'png'
      ? bytes.subarray(0, 8).toString('hex') === '89504e470d0a1a0a' && bytes.subarray(-12).toString('hex') === '0000000049454e44ae426082'
      : bytes.subarray(0, 2).toString('hex') === 'ffd8' && bytes.subarray(-2).toString('hex') === 'ffd9';
    if (!canonical || !complete) throw new Error(`${device.id}: ${image.id} contains incomplete or invalid embedded image data`);
    lines.push(`  <image href=\"${esc(styleValue(image.style, 'image'))}\" x=\"${image.x}\" y=\"${image.y}\" width=\"${image.width}\" height=\"${image.height}\" preserveAspectRatio=\"xMidYMid meet\"/>`);
  }
  for (const mask of masks) {
    lines.push(`  <rect id=\"${esc(mask.id)}\" x=\"${mask.x}\" y=\"${mask.y}\" width=\"${mask.width}\" height=\"${mask.height}\" fill=\"${esc(styleValue(mask.style, 'fillColor') || '#ffffff')}\"/>`);
  }

  const labelsViaConnector = new Set();
  for (const connector of connectors) {
    const label = byId.get(connector.source);
    const anchor = byId.get(connector.target);
    if (!label || !anchor) throw new Error(`${device.id}: ${connector.id} has a missing source or target`);
    const id = connector.id.slice('connector-'.length);
    labelsViaConnector.add(label.id);
    const ax = anchor.x + anchor.width / 2;
    const ay = anchor.y + anchor.height / 2;
    const end = endpoint(label, anchor);
    lines.push(`  <!-- callout:${id} -->`);
    if (connector.points.length) {
      const points = [{ x: ax, y: ay }, ...connector.points, end]
        .map(({ x, y }) => `${x},${y}`)
        .join(' ');
      lines.push(`  <polyline points=\"${points}\" fill=\"none\" stroke=\"#00bfff\" stroke-width=\"1.5\" stroke-dasharray=\"5 3\" opacity=\"0.75\"/>`);
    } else {
      lines.push(`  <line x1=\"${ax}\" y1=\"${ay}\" x2=\"${end.x}\" y2=\"${end.y}\" stroke=\"#00bfff\" stroke-width=\"1.5\" stroke-dasharray=\"5 3\" opacity=\"0.75\"/>`);
    }
    lines.push(`  <circle cx=\"${ax}\" cy=\"${ay}\" r=\"5\" fill=\"#00bfff\" stroke=\"#0f172a\" stroke-width=\"1.5\"/>`);
    renderLabelBox(lines, label, id, 'lbl', watermarks);
  }

  // Standalone label boxes (no callout lines) — used when the photo already has control artwork.
  for (const label of labels) {
    if (labelsViaConnector.has(label.id)) continue;
    const id = label.id.startsWith('label-') ? label.id.slice('label-'.length) : label.id;
    lines.push(`  <!-- box:${id} -->`);
    renderLabelBox(lines, label, id, 'lbl', watermarks);
  }

  // Presentation fields are editable text regions, not physical controls.
  for (const presentation of presentations) {
    const id = presentation.id.slice('presentation-'.length);
    lines.push(`  <!-- presentation:${id} -->`);
    renderLabelBox(lines, presentation, id, 'meta', watermarks);
  }

  lines.push(`  <text x=\"${width / 2}\" y=\"${height - 8}\" text-anchor=\"middle\" font-family=\"Arial,sans-serif\" font-size=\"12\" fill=\"#475569\">${esc(footer.value)}</text>`);
  lines.push('</svg>');
  const output = lines.join('\n');
  const exportedLabelIds = [...output.matchAll(/<text id="lbl-([^"]+)"/g)].map((match) => match[1]);
  if (new Set(exportedLabelIds).size !== exportedLabelIds.length) {
    const duplicates = [...new Set(exportedLabelIds.filter((id, index) => exportedLabelIds.indexOf(id) !== index))];
    throw new Error(`${device.id}: duplicate exported SVG label IDs: ${duplicates.join(', ')}`);
  }
  return output;
}

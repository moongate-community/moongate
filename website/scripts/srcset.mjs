// Locate URL tokens as HTML srcset does: commas inside a data URL belong to it.
// Return source spans so rewriting does not alter density/width descriptors.
export function srcsetUrls(value) {
  const urls = [];
  let position = 0;
  while (position < value.length) {
    while (/[\s,]/.test(value[position] ?? '') && position < value.length) position++;
    const start = position;
    while (position < value.length && !/\s/.test(value[position])) position++;
    let end = position;
    while (end > start && value[end - 1] === ',') end--;
    if (end > start) urls.push({ url: value.slice(start, end), start, end });
    if (end !== position) continue;
    let parentheses = 0;
    while (position < value.length) {
      const char = value[position++];
      if (char === '(') parentheses++;
      if (char === ')') parentheses--;
      if (char === ',' && parentheses === 0) break;
    }
  }
  return urls;
}

export function rewriteSrcset(value, resolve) {
  for (const { url, start, end } of srcsetUrls(value).reverse()) {
    value = value.slice(0, start) + resolve(url) + value.slice(end);
  }
  return value;
}

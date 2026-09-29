const search = document.querySelector<HTMLInputElement>('#packet-search');
const category = document.querySelector<HTMLSelectElement>('#packet-category');
const tag = document.querySelector<HTMLSelectElement>('#packet-tag');
const count = document.querySelector<HTMLElement>('#packet-result-count');
const empty = document.querySelector<HTMLElement>('#packet-empty');
const detailEmpty = document.querySelector<HTMLElement>('#detail-empty');
const items = [...document.querySelectorAll<HTMLElement>('[data-packet-item]')];
const details = [...document.querySelectorAll<HTMLElement>('[data-packet-detail]')];
const directionButtons = [...document.querySelectorAll<HTMLButtonElement>('[data-direction]')];

let direction = 'all';
let selected = 0;

function selectPacket(index: number, focus = false) {
  selected = index;
  for (const [position, article] of details.entries()) article.hidden = position !== index;
  for (const item of items) {
    const button = item.querySelector<HTMLButtonElement>('[data-packet-select]');
    if (!button) continue;
    if (Number(item.dataset.index) === index) button.setAttribute('aria-current', 'true');
    else button.removeAttribute('aria-current');
  }
  if (detailEmpty) detailEmpty.hidden = index !== -1;
  if (index !== -1) {
    history.replaceState(null, '', `#packet-${index}`);
    if (focus) details[index]?.querySelector<HTMLElement>('h2')?.focus();
  }
}

function applyFilters() {
  const query = search?.value.trim().toLowerCase() ?? '';
  const categoryValue = category?.value ?? 'all';
  const tagValue = tag?.value ?? 'all';
  let visible = 0;
  let first = -1;
  for (const item of items) {
    const matches = (direction === 'all' || item.dataset.direction === direction) &&
      (categoryValue === 'all' || item.dataset.category === categoryValue) &&
      (tagValue === 'all' || item.dataset.tags?.split('|').includes(tagValue)) &&
      (item.dataset.search?.includes(query) ?? false);
    item.hidden = !matches;
    if (matches) {
      visible++;
      if (first === -1) first = Number(item.dataset.index);
    }
  }
  if (count) count.textContent = `${visible} ${visible === 1 ? 'packet' : 'packets'}`;
  if (empty) empty.hidden = visible !== 0;
  if (selected === -1 || items[selected]?.hidden) selectPacket(first);
}

for (const item of items) {
  item.querySelector<HTMLButtonElement>('[data-packet-select]')?.addEventListener('click', () => {
    selectPacket(Number(item.dataset.index), true);
  });
}
for (const button of directionButtons) button.addEventListener('click', () => {
  direction = button.dataset.direction ?? 'all';
  for (const candidate of directionButtons) candidate.setAttribute('aria-pressed', String(candidate === button));
  applyFilters();
});
search?.addEventListener('input', applyFilters);
category?.addEventListener('change', applyFilters);
tag?.addEventListener('change', applyFilters);

const initialIndex = Number(/^#packet-(\d+)$/.exec(location.hash)?.[1]);
if (Number.isInteger(initialIndex) && initialIndex >= 0 && initialIndex < items.length) selectPacket(initialIndex);

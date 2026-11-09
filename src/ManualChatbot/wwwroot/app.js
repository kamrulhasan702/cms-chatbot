'use strict';
const $ = id => document.getElementById(id);
const history = [];
let selectedImage = null, uploadGeneration = 0;
let busy = false, ready = false, generation = 0, activeController = null;
async function api(path, options = {}) {
  const response = await fetch(path, { credentials: 'same-origin', ...options });
  const data = await response.json().catch(() => ({ message: 'The server returned an unexpected response.' }));
  if (!response.ok) throw new Error(data.message || 'Unable to complete the request. Please retry.');
  return data;
}
async function refreshStatus() {
  try {
    const status = await api('/api/status');
    ready = status.ready && status.manualReady;
    $('connectionText').textContent = ready ? 'Online' : 'Setup required';
    $('availability').dataset.state = ready ? 'online' : 'setup';
    $('setupNotice').hidden = status.ready;
    updateInput();
  } catch (error) {
    $('connectionText').textContent = 'Offline';
    $('availability').dataset.state = 'offline';
    $('setupNotice').textContent = error.message + ' Refresh this page to reconnect.';
    $('setupNotice').hidden = false;
  }
}
function scrollToBottom() { requestAnimationFrame(() => { $('conversation').scrollTop = $('conversation').scrollHeight; }); }
function addMessage(role, text, error = false) {
  const row = document.createElement('article'); row.className = 'message ' + role + (error ? ' error' : '');
  const content = document.createElement('div'); content.className = 'message-content';
  const bubble = document.createElement('div'); bubble.className = 'bubble'; renderMessageText(bubble, text);
  content.appendChild(bubble); row.append(content); $('messages').appendChild(row);
  scrollToBottom(); return { row, content, bubble };
}
function renderMessageText(element, text) {
  element.replaceChildren();
  for (const part of String(text).split(/(\*\*[^*\n]+\*\*)/g)) {
    if (part.startsWith('**') && part.endsWith('**') && part.length > 4) {
      const strong = document.createElement('strong'); strong.textContent = part.slice(2, -2); element.appendChild(strong);
    } else element.appendChild(document.createTextNode(part));
  }
}
function typingMessage() {
  const message = addMessage('assistant', '');
  const dots = document.createElement('div'); dots.className = 'typing'; dots.setAttribute('aria-label', 'Searching the manual');
  for (let i = 0; i < 3; i++) dots.appendChild(document.createElement('span'));
  message.bubble.appendChild(dots); return message;
}
function setBusy(value) {
  busy = value; $('send').disabled = value || !ready || !$('question').value.trim();
  $('attachImage').disabled = value; $('removeAttachment').disabled = value;
  $('send').setAttribute('aria-busy', String(value));
  $('inputHint').textContent = value ? 'Finding the right answer…' : 'Ask in your language';
}
async function ask(text, retry = false, retryImage = null) {
  text = text.trim(); if (!text || busy) return;
  if (!ready) { $('setupNotice').hidden = false; return; }
  if (text.length > 2500) return;
  const current = ++generation;
  const attachment = retry ? retryImage : selectedImage;
  if (!retry) {
    const message = addMessage('user', text);
    if (attachment) addScreenshot(message.bubble, attachment.dataUrl);
    clearAttachment();
  }
  $('question').value = ''; updateInput(); setBusy(true);
  const pending = typingMessage();
  const controller = new AbortController(); activeController = controller;
  const timer = setTimeout(() => controller.abort(), 125000);
  try {
    const data = await api('/api/chat', { method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ message: text, history: history.slice(-6), imageDataUrl: attachment?.dataUrl ?? null }), signal: controller.signal });
    if (current !== generation) return;
    renderMessageText(pending.bubble, data.answer);
    renderManualImages(pending.content, data.images, data.language);
    pending.bubble.lang = data.language === 'bn' ? 'bn' : data.language === 'banglish' ? 'bn-Latn' : 'en';
    history.push({ role: 'user', content: text }, { role: 'assistant', content: data.answer });
    if (history.length > 12) history.splice(0, history.length - 12);
  } catch (error) {
    if (current !== generation) return;
    pending.row.classList.add('error');
    pending.bubble.textContent = error.name === 'AbortError' ? 'The answer took too long. Please retry.' : error.message;
    const retryButton = document.createElement('button'); retryButton.className = 'retry-button'; retryButton.textContent = 'Try again';
    retryButton.addEventListener('click', () => { if (busy) return; pending.row.remove(); ask(text, true, attachment); });
    pending.bubble.appendChild(retryButton);
  } finally {
    clearTimeout(timer);
    if (current === generation) { activeController = null; setBusy(false); scrollToBottom(); $('question').focus(); }
  }
}
function updateInput() {
  const input = $('question'); $('charCount').textContent = input.value.length + ' / 2500';
  input.rows = Math.min(4, Math.max(1, input.value.split('\n').length));
  $('send').disabled = busy || !ready || !input.value.trim();
}
function newChat() {
  ++generation; activeController?.abort(); activeController = null; history.length = 0;
  for (const dialog of document.querySelectorAll('.image-viewer')) dialog.close();
  clearAttachment(); $('uploadError').hidden = true;
  $('messages').replaceChildren(); $('welcome').hidden = false; setBusy(false);
  $('question').value = ''; updateInput(); $('question').focus();
}
$('send').addEventListener('click', () => ask($('question').value));
$('question').addEventListener('input', updateInput);
$('question').addEventListener('keydown', e => { if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) { e.preventDefault(); ask($('question').value); } });
for (const button of document.querySelectorAll('[data-question]')) button.addEventListener('click', () => ask(button.dataset.question));
document.addEventListener('keydown', e => { if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') { e.preventDefault(); newChat(); } });
refreshStatus();

function clearAttachment() {
  ++uploadGeneration; selectedImage = null; $('imageInput').value = '';
  $('attachmentThumbnail').removeAttribute('src'); $('attachmentPreview').hidden = true;
}
function addScreenshot(bubble, dataUrl, alt = 'Uploaded CMS screenshot', append = false) {
  const button = document.createElement('button'); button.type = 'button'; button.className = 'screenshot-button';
  button.setAttribute('aria-label', alt);
  const img = document.createElement('img'); img.src = dataUrl; img.alt = alt; button.appendChild(img);
  button.addEventListener('click', () => {
    const dialog = document.createElement('dialog'); dialog.className = 'image-viewer';
    const close = document.createElement('button'); close.textContent = 'Close'; close.type = 'button';
    const full = document.createElement('img'); full.src = dataUrl; full.alt = img.alt;
    close.addEventListener('click', () => dialog.close());
    dialog.addEventListener('close', () => dialog.remove()); dialog.append(close, full); document.body.appendChild(dialog); dialog.showModal();
  });
  if (append) bubble.appendChild(button); else bubble.prepend(button);
}
$('attachImage').addEventListener('click', () => { if (!busy) $('imageInput').click(); });
$('removeAttachment').addEventListener('click', () => { if (!busy) clearAttachment(); });
async function selectImage(file) {
  if (!file || busy) return;
  clearAttachment(); const currentUpload = uploadGeneration;
  $('uploadError').hidden = true;
  try {
    if (!['image/png', 'image/jpeg', 'image/webp'].includes(file.type)) throw new Error('Choose a PNG, JPG or WebP screenshot.');
    if (file.size > 3 * 1024 * 1024) throw new Error('Screenshot must be 3 MB or smaller.');
    const dataUrl = await new Promise((resolve, reject) => {
      const reader = new FileReader(); reader.onload = () => resolve(reader.result);
      reader.onerror = () => reject(new Error('Could not read the image. Choose it again.')); reader.readAsDataURL(file);
    });
    const image = new Image(); image.src = dataUrl; await image.decode();
    if (image.width * image.height > 25000000) throw new Error('The screenshot is too large. Crop the relevant area first.');
    if (currentUpload !== uploadGeneration || busy) return;
    selectedImage = { dataUrl, name: file.name }; $('attachmentThumbnail').src = dataUrl;
    $('attachmentName').textContent = file.name; $('attachmentPreview').hidden = false; $('question').focus();
  } catch (error) {
    if (currentUpload !== uploadGeneration) return;
    $('uploadError').textContent = error.message; $('uploadError').hidden = false;
  }
 }
$('imageInput').addEventListener('change', () => selectImage($('imageInput').files[0]));
document.querySelector('.chat-window').addEventListener('paste', e => {
  const items = Array.from(e.clipboardData?.items || []);
  const item = items.find(item => item.kind === 'file' && item.type.startsWith('image/'));
  const file = item?.getAsFile() || Array.from(e.clipboardData?.files || []).find(file => file.type.startsWith('image/'));
  if (!file) return; // Normal text paste is unchanged.
  e.preventDefault();
  if (!busy) selectImage(file);
});
function renderManualImages(content, images, language) {
  if (!Array.isArray(images) || !images.length) return;
  const gallery = document.createElement('div'); gallery.className = 'manual-gallery';
  const title = document.createElement('div'); title.className = 'manual-gallery-title';
  title.textContent = language === 'bn' ? 'সংশ্লিষ্ট ছবি' : language === 'banglish' ? 'Related chobi' : 'Related images';
  gallery.appendChild(title);
  const seen = new Set();
  for (const figure of images.slice(0, 3)) {
    if (!/^\/api\/manual-images\/fig-[a-f0-9]{12}$/.test(figure.url) || seen.has(figure.url)) continue;
    seen.add(figure.url);
    const card = document.createElement('div'); card.className = 'manual-figure';
    // Use a neutral label; the PDF's extracted caption is used only as alt text.
    addScreenshot(card, figure.url, figure.caption || 'Manual illustration', true);
    const img = card.querySelector('img'); img.loading = 'lazy';
    img.addEventListener('load', scrollToBottom);
    img.addEventListener('error', () => { card.remove(); if (!gallery.querySelector('.manual-figure')) gallery.remove(); });
    gallery.appendChild(card);
  }
  if (seen.size) content.appendChild(gallery);
}

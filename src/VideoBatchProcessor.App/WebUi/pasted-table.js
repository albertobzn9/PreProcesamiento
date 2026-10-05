(() => {
  document.head.insertAdjacentHTML('beforeend', `<style>
    #paste-table-modal { z-index:21; }
    .paste-table-dialog { width:min(1050px,94vw); }
    .paste-table-body { display:flex; flex-direction:column; gap:12px; min-height:0; padding:18px 22px; overflow:auto; }
    #paste-table-text { width:100%; min-height:120px; flex:0 0 140px; resize:vertical; padding:10px; border:1px solid var(--line); border-radius:4px; font:12px monospace; white-space:pre; overflow:auto; }
    .paste-table-options { display:flex; align-items:center; flex-wrap:wrap; gap:12px; }
    .paste-table-options label, .paste-table-options select { margin:0; width:auto; }
    #paste-table-status { margin:0; font-size:13px; }
    #paste-table-status.error { color:#b42318; }
    .paste-table-preview { overflow:auto; min-height:100px; flex:1; border:1px solid var(--line); }
    .paste-table-preview table { border-collapse:collapse; width:100%; min-width:760px; font-size:12px; font-variant-numeric:tabular-nums; }
    .paste-table-preview th, .paste-table-preview td { padding:8px; border-bottom:1px solid var(--line); text-align:right; white-space:nowrap; }
    .paste-table-preview th { position:sticky; top:0; background:var(--surface); }
    #paste-table-button { width:100%; margin-top:8px; }
    @media (max-width:640px) { .paste-table-dialog .crop-dialog-footer { flex-direction:column; align-items:stretch; } .paste-table-dialog .crop-dialog-footer > div { display:flex; justify-content:flex-end; gap:8px; } }
  </style>`);
  document.getElementById('attach-source-button').insertAdjacentHTML('afterend', '<button id="paste-table-button" class="crop-button" data-action="paste-table" disabled>Paste table</button>');
  document.body.insertAdjacentHTML('beforeend', `<div id="paste-table-modal" class="crop-modal hidden" role="dialog" aria-modal="true" aria-labelledby="paste-table-title">
    <section class="crop-dialog paste-table-dialog">
      <header class="crop-dialog-head"><div><h2 id="paste-table-title">Paste behavioral table</h2><p id="paste-table-session"></p></div></header>
      <div class="paste-table-body">
        <p class="setup-help">Eight columns in MAT order: Ensayo, Lado, Estim, Latencia, TiempoAbs, PalancasIzq, PalancasDer, Desplaz. Headers are optional. Times are in seconds.</p>
        <textarea id="paste-table-text" aria-label="Excel table" spellcheck="false" placeholder="Paste the selected Excel cells here"></textarea>
        <div class="paste-table-options"><label for="paste-table-decimal">Decimal separator</label><select id="paste-table-decimal"><option value="point">Point (1.25)</option><option value="comma">Comma (1,25)</option></select><button id="preview-pasted-table" class="modal-button">Validate and preview</button></div>
        <p id="paste-table-status" role="status">No table validated.</p>
        <div class="paste-table-preview"><table aria-label="Pasted table preview"><thead></thead><tbody></tbody></table></div>
      </div>
      <footer class="crop-dialog-footer"><span class="setup-help">A local copy is kept for traceability. Original files stay unchanged.</span><div><button id="cancel-pasted-table" class="modal-button">Cancel</button><button id="save-pasted-table" class="modal-button primary" disabled>Use table</button></div></footer>
    </section>
  </div>`);

  const modal = document.getElementById('paste-table-modal');
  const input = document.getElementById('paste-table-text');
  const decimal = document.getElementById('paste-table-decimal');
  const status = document.getElementById('paste-table-status');
  const preview = document.getElementById('preview-pasted-table');
  const save = document.getElementById('save-pasted-table');
  const cancel = document.getElementById('cancel-pasted-table');
  let sessionId = null;
  let requestId = 0;
  let validated = false;
  let saving = false;
  let opener = null;

  function message(text, error = false) { status.textContent = text; status.classList.toggle('error', error); }
  function invalidate() {
    requestId++;
    validated = false;
    save.disabled = true;
    preview.disabled = !input.value.trim();
    modal.querySelector('thead').replaceChildren();
    modal.querySelector('tbody').replaceChildren();
    message('No table validated.');
  }
  function close() {
    if (saving) return;
    requestId++;
    sessionId = null;
    modal.classList.add('hidden');
    opener?.focus();
  }
  function open(id, button) {
    if (state.batchRunning || saving) return;
    const session = state.sessions.find(item => item.sessionId === id);
    if (!session?.isSourceSession) { showNotice('Select a recognized source video first.'); return; }
    sessionId = id;
    opener = button;
    cancel.disabled = input.disabled = decimal.disabled = false;
    input.value = '';
    decimal.value = 'point';
    invalidate();
    document.getElementById('paste-table-session').textContent = session.fileName;
    modal.classList.remove('hidden');
    input.focus();
  }
  function send(type) {
    if (!sessionId || saving || (type === 'savePastedTable' && !validated)) return;
    saving = type === 'savePastedTable';
    preview.disabled = true;
    save.disabled = true;
    cancel.disabled = saving;
    input.disabled = saving;
    decimal.disabled = saving;
    message(saving ? 'Saving table...' : 'Validating table...');
    sendToHost({ type, requestId: ++requestId, sessionId, text: input.value, decimalComma: decimal.value === 'comma' });
  }
  input.addEventListener('input', invalidate);
  decimal.addEventListener('change', invalidate);
  preview.addEventListener('click', () => send('previewPastedTable'));
  save.addEventListener('click', () => send('savePastedTable'));
  cancel.addEventListener('click', close);
  document.addEventListener('keydown', event => {
    if (event.key === 'Escape' && !modal.classList.contains('hidden')) { event.stopImmediatePropagation(); event.preventDefault(); close(); }
    if (event.key === 'Tab' && !modal.classList.contains('hidden')) {
      const controls = [...modal.querySelectorAll('textarea, select, button')].filter(item => !item.disabled);
      const first = controls[0];
      const last = controls.at(-1);
      if (first && event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
      else if (last && !event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
    }
  }, true);
  document.addEventListener('click', event => {
    const button = event.target.closest('[data-action="paste-table"]');
    if (button) open(button.dataset.sourceSessionId || state.selectedSessionId, button);
  });
  window.pastedTableUi = {
    updateButton() { document.getElementById('paste-table-button').disabled = !state.sessions.some(item => item.sessionId === state.selectedSessionId && item.isSourceSession) || state.batchRunning; },
    receive(response) {
      if (response.type === 'sessionsLoaded') { saving = false; close(); return false; }
      if (!['pastedTablePreview', 'pastedTableSaved', 'pastedTableRejected'].includes(response.type)) return false;
      if (response.requestId !== requestId || response.sessionId !== sessionId) return true;
      saving = false;
      cancel.disabled = input.disabled = decimal.disabled = false;
      preview.disabled = false;
      if (response.type === 'pastedTableRejected') { validated = false; save.disabled = true; message(response.message, true); return true; }
      if (response.type === 'pastedTableSaved') { close(); receiveFromHost({ type: 'behavioralSourceAttached', session: response.session }); return true; }
      const renderRow = (cells, tag) => {
        const row = document.createElement('tr');
        for (const value of cells) { const cell = document.createElement(tag); cell.textContent = value; row.append(cell); }
        return row;
      };
      modal.querySelector('thead').replaceChildren(renderRow(response.columns, 'th'));
      modal.querySelector('tbody').replaceChildren(...response.rows.map(row => renderRow(row, 'td')));
      validated = true;
      save.disabled = false;
      message(`${response.rowCount} event rows validated. ${response.rowCount > response.rows.length ? `Showing the first ${response.rows.length}; all rows will be used.` : 'Review the columns, then use the table.'}`);
      return true;
    },
  };
  window.pastedTableUi.updateButton();
})();

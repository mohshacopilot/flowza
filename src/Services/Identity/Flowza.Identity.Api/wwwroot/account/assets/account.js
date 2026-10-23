// Show/hide password on Identity's own pages (spec 007 UI-01-AC2, plan 007 PA6). Progressive enhancement: without
// JavaScript the fields simply stay hidden and the forms still work. A file, not an inline script, so the Gateway's
// Content-Security-Policy (script-src 'self') stays as it is.
//
// Each password field gets a real <button type="button"> after it. It is a toggle button: its name stays
// "Show password" and aria-pressed says whether the password is shown (WAI-ARIA: a toggle's label must not change
// with its state, or screen readers announce it wrongly). The name comes from the page (translation files).
(function () {
  'use strict';

  var label = document.body.getAttribute('data-show-password-label');
  if (!label) {
    return;
  }

  var fields = document.querySelectorAll('input[type="password"]');
  for (var i = 0; i < fields.length; i++) {
    addToggle(fields[i]);
  }

  function addToggle(input) {
    var button = document.createElement('button');
    button.type = 'button';
    button.className = 'password-toggle';
    button.textContent = label;
    button.setAttribute('aria-pressed', 'false');
    button.setAttribute('aria-controls', input.id);
    button.addEventListener('click', function () {
      var show = input.type === 'password';
      input.type = show ? 'text' : 'password';
      button.setAttribute('aria-pressed', show ? 'true' : 'false');
    });
    input.insertAdjacentElement('afterend', button);

    // Never leave a typed password readable on the page after the form is sent.
    if (input.form) {
      input.form.addEventListener('submit', function () {
        input.type = 'password';
        button.setAttribute('aria-pressed', 'false');
      });
    }
  }
})();

// Show/hide password on Identity's own pages (spec 007 UI-01-AC2, plan 007 PA6 and §5.4). Progressive enhancement:
// without JavaScript the fields simply stay hidden and the forms still work. A file, not an inline script, so the
// Gateway's Content-Security-Policy (script-src 'self') stays as it is.
//
// Each password field gets a real <button type="button"> inside it, drawn as an eye icon (mockup B). It is a toggle
// button: its name stays "Show password" and aria-pressed says whether the password is shown (WAI-ARIA: a toggle's
// label must not change with its state, or screen readers announce it wrongly). The name comes from the page
// (translation files); the icon follows the state, so sighted users see it too.
(function () {
  'use strict';

  var label = document.body.getAttribute('data-show-password-label');
  if (!label) {
    return;
  }

  // Outline icons drawn with the text colour (currentColor), so no colour is written here.
  var svgStart =
    '<svg aria-hidden="true" viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" ' +
    'stroke-width="2" stroke-linecap="round" stroke-linejoin="round">';
  var eye = svgStart + '<path d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12z"/><circle cx="12" cy="12" r="3"/></svg>';
  var eyeOff =
    svgStart +
    '<path d="M3 3l18 18M10.6 5.1A10.8 10.8 0 0 1 12 5c6.4 0 10 7 10 7a17 17 0 0 1-3.2 4.1M6.6 6.6C3.8 8.4 2 12 2 12' +
    's3.6 7 10 7a10 10 0 0 0 5.4-1.6M9.9 9.9a3 3 0 0 0 4.2 4.2"/></svg>';

  var fields = document.querySelectorAll('input[type="password"]');
  for (var i = 0; i < fields.length; i++) {
    addToggle(fields[i]);
  }

  function addToggle(input) {
    var hadFocus = document.activeElement === input;

    // One positioned box holds the field and the button, so the icon sits inside the field's right edge.
    var box = document.createElement('span');
    box.className = 'password-input';
    input.parentNode.insertBefore(box, input);
    box.appendChild(input);

    var button = document.createElement('button');
    button.type = 'button';
    button.className = 'password-toggle';
    button.setAttribute('aria-label', label);
    button.setAttribute('aria-pressed', 'false');
    button.setAttribute('aria-controls', input.id);
    button.innerHTML = eye;
    box.appendChild(button);

    function show(visible) {
      input.type = visible ? 'text' : 'password';
      button.setAttribute('aria-pressed', visible ? 'true' : 'false');
      button.innerHTML = visible ? eyeOff : eye;
    }

    button.addEventListener('click', function () {
      show(input.type === 'password');
    });

    // Never leave a typed password readable on the page after the form is sent.
    if (input.form) {
      input.form.addEventListener('submit', function () {
        show(false);
      });
    }

    // Moving a field in the page drops its focus (e.g. the change-password page's autofocus); give it back.
    if (hadFocus) {
      input.focus();
    }
  }
})();

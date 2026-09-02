/* ============================================================
   Ulak POD — ops panel vanilla helpers (jQuery available for AJAX)
   ============================================================ */
(function (window, document) {
  "use strict";

  var Ulak = {};

  /* ---------- theme ---------- */
  var THEME_KEY = "ulak-theme";
  var root = document.documentElement;

  function systemPrefersDark() {
    return window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches;
  }
  function currentTheme() {
    var explicit = root.getAttribute("data-theme");
    return explicit ? explicit : (systemPrefersDark() ? "dark" : "light");
  }
  function storedTheme() {
    try { return localStorage.getItem(THEME_KEY); } catch (e) { return null; }
  }
  function storeTheme(v) {
    try { localStorage.setItem(THEME_KEY, v); } catch (e) { /* private mode */ }
  }
  Ulak.initTheme = function () {
    var s = storedTheme();
    if (s === "dark" || s === "light") { root.setAttribute("data-theme", s); }
    var btn = document.getElementById("themeToggle");
    if (btn) {
      var sync = function () {
        var dark = currentTheme() === "dark";
        btn.setAttribute("aria-label", dark ? btn.dataset.labelLight : btn.dataset.labelDark);
        btn.title = dark ? btn.dataset.labelLight : btn.dataset.labelDark;
      };
      sync();
      btn.addEventListener("click", function () {
        var next = currentTheme() === "dark" ? "light" : "dark";
        root.setAttribute("data-theme", next);
        storeTheme(next);
        sync();
      });
    }
  };

  /* ---------- csrf ---------- */
  Ulak.csrf = function () {
    var el = document.querySelector('input[name="__RequestVerificationToken"]');
    return el ? el.value : "";
  };

  /* ---------- modal ---------- */
  Ulak.modal = function (id) {
    var backdrop = typeof id === "string" ? document.getElementById(id) : id;
    if (!backdrop) return { open: function () {}, close: function () {} };
    function close() { backdrop.classList.remove("is-open"); }
    function open() { backdrop.classList.add("is-open"); }
    backdrop.addEventListener("click", function (e) { if (e.target === backdrop) close(); });
    backdrop.querySelectorAll("[data-close]").forEach(function (b) {
      b.addEventListener("click", close);
    });
    document.addEventListener("keydown", function (e) {
      if (e.key === "Escape" && backdrop.classList.contains("is-open")) close();
    });
    return { open: open, close: close, el: backdrop };
  };

  /* ---------- html escape ---------- */
  Ulak.esc = function (s) {
    if (s === null || s === undefined) return "";
    return String(s).replace(/[&<>"']/g, function (c) {
      return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c];
    });
  };

  var ICON_PREV = '<svg viewBox="0 0 24 24" fill="none"><path d="M15 6l-6 6 6 6" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>';
  var ICON_NEXT = '<svg viewBox="0 0 24 24" fill="none"><path d="M9 6l6 6-6 6" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>';

  /* ---------- server-side paged table ----------
     opts: {
       body:   selector/element of <tbody>
       foot:   selector/element of the .table-foot container (has .count + .pager)
       url:    data endpoint returning { data: [], totalCount: n }
       pageSize, sort, desc,
       filters:  () => ({...})   extra query params
       render:   (row) => "<tr>...</tr>"
       onRowClick: (row) => void   (optional)
       colspan:  number  (empty-state colspan)
       text:     { empty, error, page, records }   localized strings
     }
     returns { refresh() }
  */
  Ulak.dataTable = function (opts) {
    var body = typeof opts.body === "string" ? document.querySelector(opts.body) : opts.body;
    var foot = typeof opts.foot === "string" ? document.querySelector(opts.foot) : opts.foot;
    var countEl = foot ? foot.querySelector(".count") : null;
    var pagerEl = foot ? foot.querySelector(".pager") : null;
    var text = opts.text || {};
    var pageSize = opts.pageSize || 20;
    var page = 0;
    var colspan = opts.colspan || 6;

    function query() {
      var q = Object.assign({}, typeof opts.filters === "function" ? opts.filters() : {});
      q.skip = page * pageSize;
      q.take = pageSize;
      q.sort = opts.sort;
      q.desc = !!opts.desc;
      return q;
    }

    function renderPager(total) {
      if (!pagerEl) return;
      var pages = Math.max(1, Math.ceil(total / pageSize));
      if (page >= pages) page = pages - 1;
      pagerEl.innerHTML = "";
      var prev = document.createElement("button");
      prev.innerHTML = ICON_PREV;
      prev.setAttribute("aria-label", text.prev || "Previous");
      prev.disabled = page <= 0;
      prev.addEventListener("click", function () { if (page > 0) { page--; load(); } });
      var ind = document.createElement("button");
      ind.className = "page-ind";
      ind.textContent = (page + 1) + " / " + pages;
      ind.disabled = true;
      var next = document.createElement("button");
      next.innerHTML = ICON_NEXT;
      next.setAttribute("aria-label", text.next || "Next");
      next.disabled = page >= pages - 1;
      next.addEventListener("click", function () { if (page < pages - 1) { page++; load(); } });
      pagerEl.appendChild(prev);
      pagerEl.appendChild(ind);
      pagerEl.appendChild(next);
      if (countEl) {
        countEl.textContent = (text.records
          ? text.records.replace("{0}", total)
          : (total + " records"));
      }
    }

    function load() {
      if (!body) return;
      body.innerHTML = '<tr><td class="cell-empty" colspan="' + colspan + '">…</td></tr>';
      window.jQuery.getJSON(opts.url, query())
        .done(function (r) {
          var rows = (r && r.data) || [];
          var total = (r && r.totalCount) || 0;
          if (!rows.length) {
            body.innerHTML = '<tr><td class="cell-empty" colspan="' + colspan + '">' +
              Ulak.esc(text.empty || "No records.") + "</td></tr>";
          } else {
            body.innerHTML = rows.map(opts.render).join("");
            if (typeof opts.onRowClick === "function") {
              Array.prototype.forEach.call(body.querySelectorAll("tr"), function (tr, i) {
                tr.classList.add("is-clickable");
                tr.addEventListener("click", function () { opts.onRowClick(rows[i]); });
              });
            }
          }
          renderPager(total);
        })
        .fail(function () {
          body.innerHTML = '<tr><td class="cell-empty" colspan="' + colspan + '">' +
            Ulak.esc(text.error || "Failed to load.") + "</td></tr>";
        });
    }

    load();
    return { refresh: function () { page = 0; load(); } };
  };

  /* ---------- 7-day delivered/failed bar chart (inline SVG) ---------- */
  Ulak.drawBarChart = function (svg, days, text) {
    if (!svg || !days) return;
    var svgNS = "http://www.w3.org/2000/svg";
    var wrap = svg.closest(".chart-wrap");
    var tooltip = wrap ? wrap.querySelector(".chart-tooltip") : null;
    text = text || {};
    var W = 640, H = 220, padL = 26, padR = 12, padT = 14, padB = 30;
    var plotW = W - padL - padR, plotH = H - padT - padB;
    var maxVal = Math.max(1, days.reduce(function (m, d) { return Math.max(m, d.ok, d.fail); }, 0));
    var groupW = plotW / days.length;
    var barW = 15, gap = 4;

    function el(name, attrs) {
      var e = document.createElementNS(svgNS, name);
      for (var k in attrs) { e.setAttribute(k, attrs[k]); }
      return e;
    }
    svg.innerHTML = "";
    var ticks = niceTicks(maxVal);
    ticks.forEach(function (v) {
      var y = padT + plotH - (v / ticks[ticks.length - 1]) * plotH;
      svg.appendChild(el("line", { x1: padL, x2: W - padR, y1: y, y2: y, "class": v === 0 ? "axis-line" : "grid-line" }));
      var t = el("text", { x: padL - 8, y: y + 3, "class": "axis-label", "text-anchor": "end" });
      t.textContent = v;
      svg.appendChild(t);
    });

    function tip(day) {
      return "<b>" + Ulak.esc(day.d) + "</b> · " +
        Ulak.esc(text.delivered || "Delivered") + ": " + day.ok + " · " +
        Ulak.esc(text.failed || "Failed") + ": " + day.fail;
    }
    function moveTip(e) {
      if (!tooltip || !wrap) return;
      var r = wrap.getBoundingClientRect();
      tooltip.style.left = (e.clientX - r.left) + "px";
      tooltip.style.top = (e.clientY - r.top - 10) + "px";
    }

    days.forEach(function (day, i) {
      var gx = padL + i * groupW + groupW / 2;
      var top = ticks[ticks.length - 1];
      var baseY = padT + plotH;

      var hit = el("rect", { x: gx - barW - gap / 2 - 2, y: padT, width: barW * 2 + gap + 4, height: plotH, fill: "transparent" });
      hit.addEventListener("mouseenter", function () { if (tooltip) { tooltip.innerHTML = tip(day); tooltip.classList.add("is-visible"); } });
      hit.addEventListener("mousemove", moveTip);
      hit.addEventListener("mouseleave", function () { if (tooltip) tooltip.classList.remove("is-visible"); });
      svg.appendChild(hit);

      [["ok", gx - barW - gap / 2, "bar"], ["fail", gx + gap / 2, "bar is-fail"]].forEach(function (spec) {
        var val = day[spec[0]];
        var h = val > 0 ? Math.max((val / top) * plotH, 3) : 0.001;
        var rect = el("rect", { x: spec[1], y: baseY - h, width: barW, height: h, rx: h > 3 ? 3 : 0, "class": spec[2] });
        rect.addEventListener("mouseenter", function () { rect.classList.add("is-hovered"); if (tooltip) { tooltip.innerHTML = tip(day); tooltip.classList.add("is-visible"); } });
        rect.addEventListener("mousemove", moveTip);
        rect.addEventListener("mouseleave", function () { rect.classList.remove("is-hovered"); if (tooltip) tooltip.classList.remove("is-visible"); });
        svg.appendChild(rect);
      });

      if (day.ok > 0) {
        var lbl = el("text", { x: gx - barW - gap / 2 + barW / 2, y: baseY - (day.ok / top) * plotH - 6, "class": "bar-total-label", "text-anchor": "middle" });
        lbl.textContent = day.ok;
        svg.appendChild(lbl);
      }
      var xt = el("text", { x: gx, y: H - 9, "class": "axis-label", "text-anchor": "middle" });
      xt.textContent = day.d;
      svg.appendChild(xt);
    });
  };

  function niceTicks(max) {
    var step = Math.max(1, Math.ceil(max / 4));
    var top = step * 4;
    var out = [];
    for (var v = 0; v <= top; v += step) out.push(v);
    return out;
  }

  window.Ulak = Ulak;

  document.addEventListener("DOMContentLoaded", function () {
    Ulak.initTheme();
  });
})(window, document);

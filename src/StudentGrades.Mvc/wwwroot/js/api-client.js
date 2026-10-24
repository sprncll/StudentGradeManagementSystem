// Shared helper for calling the Student Grade Web API with fetch() + async/await.
// Every request sends the signed-in user's JWT as "Authorization: Bearer <token>".
// The API (not this file) decides what the user is allowed to do.
(function () {
    'use strict';

    const configEl = document.getElementById('api-config');
    const API_BASE = configEl ? configEl.dataset.apiBase : '';
    const TOKEN = configEl ? configEl.dataset.token : '';

    /** Error thrown for any non-2xx response or network failure. */
    class ApiError extends Error {
        constructor(status, message, fieldErrors) {
            super(message);
            this.name = 'ApiError';
            this.status = status;              // 0 = network failure
            this.fieldErrors = fieldErrors || {}; // { Grade: ["..."], ... } from 400 responses
        }
    }

    function defaultMessage(status) {
        switch (status) {
            case 400: return 'The request was rejected. Please check the highlighted fields.';
            case 401: return 'Your session has expired or you are not signed in. Please sign in again.';
            case 403: return 'You are not authorized to perform this action.';
            case 404: return 'The requested record was not found.';
            case 405: return 'This action is not allowed.';
            case 409: return 'This record conflicts with an existing record.';
            default: return status >= 500
                ? 'The server encountered an error. Please try again later.'
                : 'The request failed (HTTP ' + status + ').';
        }
    }

    /**
     * Calls the API and returns the parsed JSON body (or null for 204 No Content).
     * Throws ApiError with a user-friendly message for every failure.
     */
    async function apiRequest(path, options) {
        options = options || {};
        const headers = { 'Accept': 'application/json' };
        if (TOKEN) headers['Authorization'] = 'Bearer ' + TOKEN;

        const init = { method: options.method || 'GET', headers: headers };
        if (options.body !== undefined) {
            headers['Content-Type'] = 'application/json';
            init.body = JSON.stringify(options.body);
        }

        let response;
        try {
            response = await fetch(API_BASE + path, init);
        } catch (networkError) {
            throw new ApiError(0, 'Cannot reach the Grade API at ' + API_BASE +
                '. Make sure the API is running and try again.');
        }

        if (response.status === 204) return null;

        // Parse JSON if there is any; tolerate empty or non-JSON bodies.
        let data = null;
        const text = await response.text();
        if (text) {
            try { data = JSON.parse(text); } catch (e) { data = null; }
        }

        if (!response.ok) {
            const detail = data && (data.detail || data.title);
            let message = defaultMessage(response.status);
            // Prefer the API's own explanation, except for 401/403 where our wording is clearer.
            if (detail && response.status !== 401 && response.status !== 403) message = detail;
            if (response.status === 403 && data && data.detail) message = data.detail;
            throw new ApiError(response.status, message, data && data.errors);
        }
        return data;
    }

    /** Escapes text for safe insertion into HTML (prevents XSS from database values). */
    function escapeHtml(value) {
        return String(value === null || value === undefined ? '' : value)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    /** Shows a Bootstrap alert inside the given container. */
    function showAlert(container, type, message, extraHtml) {
        if (!container) return;
        container.innerHTML =
            '<div class="alert alert-' + type + ' alert-dismissible fade show" role="alert">' +
            escapeHtml(message) + (extraHtml || '') +
            '<button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button></div>';
    }

    function clearAlert(container) { if (container) container.innerHTML = ''; }

    /** Extra HTML for 401 errors: a link back to the login page. */
    function signInLink(error) {
        return error && error.status === 401
            ? ' <a href="/Account/Login" class="alert-link">Sign in again</a>.'
            : '';
    }

    const REMARK_CLASSES = {
        'CMS': 'rm-cms',
        'SP': 'rm-sp',
        'SAP': 'rm-sap',
        'GP': 'rm-gp',
        'EP': 'rm-ep',
        'BEP/FAILED': 'rm-failed'
    };
    function remarkBadge(remarks) {
        const cls = REMARK_CLASSES[remarks] || 'rm-empty';
        return '<span class="rm ' + cls + '">' + escapeHtml(remarks) + '</span>';
    }

    /** Small slide-in notification (bottom right). type: 'success' | 'danger'. */
    function toast(type, message) {
        const area = document.getElementById('toast-area');
        if (!area) return;
        const el = document.createElement('div');
        el.className = 'ust-toast ust-toast-' + type;
        el.setAttribute('role', type === 'danger' ? 'alert' : 'status');
        el.innerHTML = '<span class="ust-toast-msg"></span><button type="button" class="ust-toast-x" aria-label="Dismiss">&times;</button>';
        el.querySelector('.ust-toast-msg').textContent = message;
        const close = () => { el.classList.add('out'); setTimeout(() => el.remove(), 250); };
        el.querySelector('.ust-toast-x').addEventListener('click', close);
        area.appendChild(el);
        setTimeout(close, 5000);
    }

    /** Animates a number from its previous value to the target. */
    function countUp(el, target, decimals, suffix) {
        if (!el) return;
        decimals = decimals || 0;
        suffix = suffix || '';
        const reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        const from = parseFloat(el.dataset.v) || 0;
        el.dataset.v = target;
        if (reduce || !isFinite(target)) {
            el.textContent = isFinite(target) ? target.toFixed(decimals) + suffix : '–';
            return;
        }
        const start = performance.now(), duration = 600;
        function step(now) {
            const t = Math.min(1, (now - start) / duration);
            const eased = 1 - Math.pow(1 - t, 3);
            el.textContent = (from + (target - from) * eased).toFixed(decimals) + suffix;
            if (t < 1) requestAnimationFrame(step);
        }
        requestAnimationFrame(step);
    }

    function formatDate(value) {
        if (!value) return '';
        // API returns UTC timestamps; append Z if the server omitted the offset.
        const text = /Z|[+-]\d\d:\d\d$/.test(value) ? value : value + 'Z';
        const d = new Date(text);
        return isNaN(d.getTime()) ? '' : d.toLocaleString();
    }

    window.GradeApi = {
        request: apiRequest,
        ApiError: ApiError,
        escapeHtml: escapeHtml,
        showAlert: showAlert,
        clearAlert: clearAlert,
        signInLink: signInLink,
        remarkBadge: remarkBadge,
        toast: toast,
        countUp: countUp,
        formatDate: formatDate
    };
})();
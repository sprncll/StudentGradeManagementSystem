// Student dashboard: read-only view of the signed-in student's own grades.
// Note there is no student ID anywhere in these requests: the API works out who the
// student is from the JWT, so a student cannot ask for someone else's grades.
(function () {
    'use strict';

    const api = window.GradeApi;
    const $ = id => document.getElementById(id);
    const els = {
        alert: $('alert-area'),
        body: $('grades-body'),
        head: $('grades-head'),
        name: $('student-name'),
        number: $('student-number'),
        standing: $('standing'),
        summary: $('summary'),
        sumCount: $('sum-count'),
        sumAverage: $('sum-average'),
        sumHigh: $('sum-high'),
        sumFailed: $('sum-failed'),
        chips: $('term-chips'),
        search: $('grade-search'),
        resultCount: $('result-count'),
        lastLoaded: $('last-loaded'),
        refresh: $('btn-refresh'),
        print: $('btn-print')
    };

    let allGrades = [];
    let term = '';
    let query = '';
    let sort = { key: null, dir: 'asc' };

    const termOf = g => g.semester + ' · ' + g.schoolYear;

    function sortValue(g, key) {
        switch (key) {
            case 'subject': return String(g.subjectCode || '').toLowerCase();
            case 'grade': return Number(g.grade);
            case 'term': return String(g.schoolYear || '') + String(g.semester || '');
            default: return 0;
        }
    }

    function visibleRows() {
        const q = query.toLowerCase();
        const rows = allGrades.filter(g =>
            (!term || termOf(g) === term) &&
            (!q || [g.subjectCode, g.subjectName, g.facultyName].some(v => String(v || '').toLowerCase().includes(q))));
        if (sort.key) {
            const dir = sort.dir === 'asc' ? 1 : -1;
            rows.sort((a, b) => {
                const x = sortValue(a, sort.key), y = sortValue(b, sort.key);
                return (x < y ? -1 : x > y ? 1 : 0) * dir;
            });
        }
        return rows;
    }

    function updateSortIndicators() {
        els.head.querySelectorAll('th[data-col]').forEach(th => {
            th.setAttribute('aria-sort', th.dataset.col === sort.key
                ? (sort.dir === 'asc' ? 'ascending' : 'descending')
                : 'none');
        });
    }

    function skeletonRows() {
        let html = '';
        for (let i = 0; i < 5; i++) {
            let cells = '';
            for (let c = 0; c < 7; c++) {
                cells += '<td><span class="skeleton" style="width:' + (50 + ((i + c) % 4) * 12) + '%"></span></td>';
            }
            html += '<tr>' + cells + '</tr>';
        }
        els.body.innerHTML = html;
    }

    function setTableMessage(message, cls) {
        els.body.innerHTML = '<tr><td colspan="7" class="text-center py-4 ' + cls + '">' + api.escapeHtml(message) + '</td></tr>';
    }

    function setEmptyState(icon, title, hint) {
        els.body.innerHTML = '<tr><td colspan="7"><div class="empty-state"><div class="empty-icon">' + icon + '</div>' +
            '<div class="fw-semibold text-dark">' + api.escapeHtml(title) + '</div>' +
            '<div class="small">' + api.escapeHtml(hint) + '</div></div></td></tr>';
    }

    function renderChips() {
        const e = api.escapeHtml;
        const counts = new Map();
        allGrades.forEach(g => counts.set(termOf(g), (counts.get(termOf(g)) || 0) + 1));
        if (counts.size <= 1) { els.chips.innerHTML = ''; return; }
        let html = '<button type="button" class="chip' + (term === '' ? ' active' : '') + '" data-term="" aria-pressed="' + (term === '') + '">All terms<span class="chip-count">' + allGrades.length + '</span></button>';
        counts.forEach((n, t) => {
            html += '<button type="button" class="chip' + (term === t ? ' active' : '') + '" data-term="' + e(t) + '" aria-pressed="' + (term === t) + '">' + e(t) + '<span class="chip-count">' + n + '</span></button>';
        });
        els.chips.innerHTML = html;
    }

    function updateSummary(rows) {
        if (rows.length === 0) {
            els.sumAverage.textContent = '–';
            els.sumHigh.textContent = '–';
            api.countUp(els.sumCount, 0, 0);
            api.countUp(els.sumFailed, 0, 0);
            els.standing.hidden = true;
            return;
        }
        const total = rows.reduce((sum, g) => sum + g.grade, 0);
        const failed = rows.filter(g => g.remarks === 'BEP/FAILED').length;
        api.countUp(els.sumCount, rows.length, 0);
        api.countUp(els.sumAverage, total / rows.length, 2);
        api.countUp(els.sumHigh, Math.max.apply(null, rows.map(g => g.grade)), 0);
        api.countUp(els.sumFailed, failed, 0);
        els.sumFailed.classList.toggle('text-danger', failed > 0);

        els.standing.hidden = false;
        els.standing.classList.toggle('warn', failed > 0);
        els.standing.textContent = failed === 0
            ? 'All subjects passed'
            : failed + ' subject' + (failed === 1 ? '' : 's') + ' need attention';
    }

    function render() {
        const e = api.escapeHtml;
        updateSortIndicators();
        renderChips();

        if (allGrades.length === 0) {
            els.summary.hidden = true;
            els.standing.hidden = true;
            els.resultCount.textContent = '';
            setEmptyState('☰', 'No grades posted yet', 'Please check back later.');
            return;
        }

        const rows = visibleRows();
        els.summary.hidden = false;
        updateSummary(rows);

        if (rows.length === 0) {
            els.resultCount.textContent = '0 of ' + allGrades.length + ' subject(s) shown';
            setEmptyState('?', 'No subjects match your filter', 'Try a different term or search.');
            return;
        }

        els.body.innerHTML = rows.map((g, i) => {
            const pct = Math.max(0, Math.min(100, Number(g.grade) || 0));
            return '<tr class="row-in' + (g.remarks === 'BEP/FAILED' ? ' row-failed' : '') + '" style="animation-delay:' + Math.min(i, 12) * 30 + 'ms">' +
                '<td class="fw-semibold">' + e(g.subjectCode) + '</td>' +
                '<td>' + e(g.subjectName) + '</td>' +
                '<td class="grade-cell"><div class="grade-num">' + e(g.grade) + '</div><div class="grade-bar' + (pct < 75 ? ' low' : '') + '"><span style="width:' + pct + '%"></span></div></td>' +
                '<td>' + api.remarkBadge(g.remarks) + '</td>' +
                '<td>' + e(g.semester) + '</td>' +
                '<td>' + e(g.schoolYear) + '</td>' +
                '<td class="small text-muted">' + e(g.facultyName) + '</td>' +
                '</tr>';
        }).join('');
        els.resultCount.textContent = rows.length === allGrades.length
            ? rows.length + ' subject(s)'
            : 'Showing ' + rows.length + ' of ' + allGrades.length + ' subject(s)';
    }

    async function load() {
        api.clearAlert(els.alert);
        skeletonRows();
        els.refresh.disabled = true;
        try {
            // Two independent API calls in parallel.
            const [profile, grades] = await Promise.all([
                api.request('/api/student/me'),
                api.request('/api/student/grades')
            ]);
            els.name.textContent = profile.fullName;
            els.number.textContent = profile.studentNumber;
            allGrades = Array.isArray(grades) ? grades : [];
            if (term && !allGrades.some(g => termOf(g) === term)) term = '';
            render();
            els.lastLoaded.textContent = 'Loaded ' + new Date().toLocaleTimeString();
        } catch (error) {
            els.summary.hidden = true;
            els.standing.hidden = true;
            let message = error.message;
            if (error.status === 403) message = 'You are not authorized to view these grades. ' + error.message;
            setTableMessage('Your grades could not be loaded.', 'text-danger');
            api.showAlert(els.alert, 'danger', message, api.signInLink(error));
        } finally {
            els.refresh.disabled = false;
        }
    }

    els.refresh.addEventListener('click', load);
    els.print.addEventListener('click', () => window.print());
    els.search.addEventListener('input', () => { query = els.search.value.trim(); render(); });
    els.chips.addEventListener('click', event => {
        const chip = event.target.closest('.chip');
        if (!chip) return;
        term = chip.dataset.term;
        render();
    });
    els.head.addEventListener('click', event => {
        const btn = event.target.closest('.th-btn');
        if (!btn) return;
        const key = btn.dataset.sort;
        sort = sort.key === key
            ? { key: key, dir: sort.dir === 'asc' ? 'desc' : 'asc' }
            : { key: key, dir: key === 'grade' ? 'desc' : 'asc' };
        render();
    });

    load();
})();
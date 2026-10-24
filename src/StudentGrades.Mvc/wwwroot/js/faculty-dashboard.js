// Faculty dashboard: view, add, edit and delete grade records through the Web API.
(function () {
    'use strict';

    const api = window.GradeApi;
    const $ = id => document.getElementById(id);
    const els = {
        alert: $('alert-area'),
        body: $('grades-body'),
        head: $('grades-head'),
        count: $('grades-count'),
        filterForm: $('filter-form'),
        filterSearch: $('filter-search'),
        filterSemester: $('filter-semester'),
        filterYear: $('filter-year'),
        clearButton: $('btn-clear'),
        addButton: $('btn-add'),
        stTotal: $('st-total'),
        stAvg: $('st-avg'),
        stPass: $('st-pass'),
        stFailed: $('st-failed'),
        failedTile: $('st-failed-tile'),
        modalEl: $('grade-modal'),
        modalTitle: $('grade-modal-title'),
        form: $('grade-form'),
        formAlert: $('form-alert'),
        saveButton: $('btn-save'),
        gradeId: $('grade-id'),
        studentId: $('f-studentId'),
        subjectCode: $('f-subjectCode'),
        subjectName: $('f-subjectName'),
        grade: $('f-grade'),
        semester: $('f-semester'),
        schoolYear: $('f-schoolYear'),
        remarkPreview: $('remark-preview'),
        deleteModalEl: $('delete-modal'),
        deleteDetail: $('delete-detail'),
        deleteConfirm: $('btn-confirm-delete')
    };
    const modal = new bootstrap.Modal(els.modalEl);
    const deleteModal = new bootstrap.Modal(els.deleteModalEl);

    let allGrades = [];
    let gradesById = new Map();
    let sort = { key: 'datePosted', dir: 'desc' };
    let failedOnly = false;
    let pendingDeleteId = null;
    let loadSeq = 0;
    let debounceTimer = null;

    // ---------------------------------------------------------------- helpers

    function remarkFor(n) {
        if (n >= 95) return 'CMS';
        if (n >= 90) return 'SP';
        if (n >= 85) return 'SAP';
        if (n >= 80) return 'GP';
        if (n >= 75) return 'EP';
        return 'BEP/FAILED';
    }

    function sortValue(g, key) {
        switch (key) {
            case 'student': return String(g.studentName || '').toLowerCase();
            case 'subject': return String(g.subjectCode || '').toLowerCase();
            case 'grade': return Number(g.grade);
            case 'semester': return String(g.semester || '');
            case 'schoolYear': return String(g.schoolYear || '');
            case 'datePosted': return new Date(g.datePosted).getTime() || 0;
            default: return 0;
        }
    }

    function visibleRows() {
        const rows = failedOnly ? allGrades.filter(g => g.remarks === 'BEP/FAILED') : allGrades.slice();
        const dir = sort.dir === 'asc' ? 1 : -1;
        rows.sort((a, b) => {
            const x = sortValue(a, sort.key), y = sortValue(b, sort.key);
            return (x < y ? -1 : x > y ? 1 : 0) * dir;
        });
        return rows;
    }

    function updateSortIndicators() {
        els.head.querySelectorAll('th[data-col]').forEach(th => {
            th.setAttribute('aria-sort', th.dataset.col === sort.key
                ? (sort.dir === 'asc' ? 'ascending' : 'descending')
                : 'none');
        });
    }

    // ---------------------------------------------------------------- loading

    async function loadLookups() {
        try {
            const [students, semesters] = await Promise.all([
                api.request('/api/lookups/students'),
                api.request('/api/lookups/semesters')
            ]);
            for (const s of students) {
                els.studentId.add(new Option(s.studentNumber + ' — ' + s.fullName, s.studentId));
            }
            for (const sem of semesters) {
                els.semester.add(new Option(sem, sem));
                els.filterSemester.add(new Option(sem, sem));
            }
        } catch (error) {
            api.showAlert(els.alert, 'danger', 'Could not load the student list: ' + error.message, api.signInLink(error));
        }
    }

    function skeletonRows() {
        let html = '';
        for (let i = 0; i < 5; i++) {
            let cells = '';
            for (let c = 0; c < 8; c++) {
                cells += '<td><span class="skeleton" style="width:' + (c === 7 ? 70 : 50 + ((i + c) % 4) * 12) + '%"></span></td>';
            }
            html += '<tr>' + cells + '</tr>';
        }
        els.body.innerHTML = html;
    }

    async function loadGrades(flashId) {
        const seq = ++loadSeq;
        const params = new URLSearchParams();
        if (els.filterSearch.value.trim()) params.set('search', els.filterSearch.value.trim());
        if (els.filterSemester.value) params.set('semester', els.filterSemester.value);
        if (els.filterYear.value.trim()) params.set('schoolYear', els.filterYear.value.trim());
        const query = params.toString() ? '?' + params.toString() : '';

        skeletonRows();
        try {
            const grades = await api.request('/api/grades' + query);
            if (seq !== loadSeq) return; // a newer request replaced this one
            allGrades = grades;
            gradesById = new Map(grades.map(g => [g.gradeId, g]));
            api.clearAlert(els.alert);
            updateStats();
            render();
            if (flashId) flashRow(flashId);
        } catch (error) {
            if (seq !== loadSeq) return;
            setTableMessage('Grade records could not be loaded.', 'text-danger');
            els.count.textContent = '';
            api.showAlert(els.alert, 'danger', error.message, api.signInLink(error));
        }
    }

    function setTableMessage(message, cls) {
        els.body.innerHTML = '<tr><td colspan="8" class="text-center py-4 ' + cls + '">' + api.escapeHtml(message) + '</td></tr>';
    }

    function setEmptyState(icon, title, hint) {
        els.body.innerHTML = '<tr><td colspan="8"><div class="empty-state"><div class="empty-icon">' + icon + '</div>' +
            '<div class="fw-semibold text-dark">' + api.escapeHtml(title) + '</div>' +
            '<div class="small">' + api.escapeHtml(hint) + '</div></div></td></tr>';
    }

    function updateStats() {
        const n = allGrades.length;
        const failed = allGrades.filter(g => g.remarks === 'BEP/FAILED').length;
        const passed = allGrades.filter(g => g.grade >= 75).length;
        api.countUp(els.stTotal, n, 0);
        api.countUp(els.stFailed, failed, 0);
        if (n === 0) {
            els.stAvg.textContent = '–';
            els.stPass.textContent = '–';
            return;
        }
        api.countUp(els.stAvg, allGrades.reduce((s, g) => s + g.grade, 0) / n, 2);
        api.countUp(els.stPass, Math.round(passed * 100 / n), 0, '%');
    }

    function render() {
        updateSortIndicators();
        if (allGrades.length === 0) {
            setEmptyState('☰', 'No grade records found', 'Use "Add grade" to post one, or clear your filters.');
            els.count.textContent = '';
            return;
        }
        const rows = visibleRows();
        if (rows.length === 0) {
            setEmptyState('✓', 'No failed records in this view', 'Click the Failed tile again to show everything.');
            els.count.textContent = '0 of ' + allGrades.length + ' record(s) shown';
            return;
        }
        const e = api.escapeHtml;
        els.body.innerHTML = rows.map((g, i) => {
            const pct = Math.max(0, Math.min(100, Number(g.grade) || 0));
            return '<tr class="row-in' + (g.remarks === 'BEP/FAILED' ? ' row-failed' : '') + '" data-row="' + e(g.gradeId) + '" style="animation-delay:' + Math.min(i, 12) * 30 + 'ms">' +
                '<td><div class="fw-semibold">' + e(g.studentNumber) + '</div><div class="small text-muted">' + e(g.studentName) + '</div></td>' +
                '<td><div class="fw-semibold">' + e(g.subjectCode) + '</div><div class="small text-muted">' + e(g.subjectName) + '</div></td>' +
                '<td class="grade-cell"><div class="grade-num">' + e(g.grade) + '</div><div class="grade-bar' + (pct < 75 ? ' low' : '') + '"><span style="width:' + pct + '%"></span></div></td>' +
                '<td>' + api.remarkBadge(g.remarks) + '</td>' +
                '<td>' + e(g.semester) + '</td>' +
                '<td>' + e(g.schoolYear) + '</td>' +
                '<td class="small text-muted">' + e(api.formatDate(g.datePosted)) +
                (g.dateModified ? '<br/>Edited ' + e(api.formatDate(g.dateModified)) : '') +
                '<br/>by ' + e(g.facultyName) + '</td>' +
                '<td class="text-end text-nowrap">' +
                '<button type="button" class="btn btn-sm btn-outline-primary me-1" data-action="edit" data-id="' + e(g.gradeId) + '">Edit</button>' +
                '<button type="button" class="btn btn-sm btn-outline-danger" data-action="delete" data-id="' + e(g.gradeId) + '">Delete</button>' +
                '</td>' +
                '</tr>';
        }).join('');
        els.count.textContent = rows.length === allGrades.length
            ? rows.length + ' record(s)'
            : 'Showing ' + rows.length + ' of ' + allGrades.length + ' record(s)';
    }

    function flashRow(id) {
        const row = els.body.querySelector('tr[data-row="' + id + '"]');
        if (!row) return;
        row.classList.add('row-flash');
        row.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
    }

    // ---------------------------------------------------------------- form

    function clearFieldErrors() {
        api.clearAlert(els.formAlert);
        els.form.querySelectorAll('.is-invalid').forEach(el => el.classList.remove('is-invalid'));
        els.form.querySelectorAll('.invalid-feedback').forEach(el => { el.textContent = ''; });
    }

    function showFieldErrors(fieldErrors) {
        let shown = false;
        for (const [field, messages] of Object.entries(fieldErrors || {})) {
            const feedback = els.form.querySelector('.invalid-feedback[data-for="' + field + '"]');
            if (!feedback) continue;
            feedback.textContent = messages.join(' ');
            const input = feedback.previousElementSibling;
            if (input) input.classList.add('is-invalid');
            shown = true;
        }
        return shown;
    }

    function updateRemarkPreview() {
        const text = els.grade.value.trim();
        const n = Number(text);
        const valid = text !== '' && Number.isInteger(n) && n >= 0 && n <= 100;
        els.remarkPreview.innerHTML = valid ? api.remarkBadge(remarkFor(n)) : '<span class="rm rm-empty">—</span>';
    }

    function openForm(grade) {
        clearFieldErrors();
        els.form.reset();
        if (grade) {
            els.modalTitle.textContent = 'Edit grade #' + grade.gradeId;
            els.gradeId.value = grade.gradeId;
            els.studentId.value = String(grade.studentId);
            els.subjectCode.value = grade.subjectCode;
            els.subjectName.value = grade.subjectName;
            els.grade.value = grade.grade;
            els.semester.value = grade.semester;
            els.schoolYear.value = grade.schoolYear;
        } else {
            els.modalTitle.textContent = 'Add grade';
            els.gradeId.value = '';
            els.schoolYear.value = '2026-2027';
        }
        updateRemarkPreview();
        modal.show();
    }

    /** Reads the form. The grade is sent exactly as typed (no rounding) so the API validates it. */
    function readForm() {
        const gradeText = els.grade.value.trim();
        return {
            studentId: els.studentId.value ? Number(els.studentId.value) : null,
            subjectCode: els.subjectCode.value.trim(),
            subjectName: els.subjectName.value.trim(),
            grade: gradeText === '' ? null : Number(gradeText),
            semester: els.semester.value,
            schoolYear: els.schoolYear.value.trim()
            // No "remarks": the server computes it.
        };
    }

    async function saveForm(event) {
        event.preventDefault();
        clearFieldErrors();

        const id = els.gradeId.value;
        const body = readForm();
        els.saveButton.disabled = true;
        els.saveButton.innerHTML = '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>Saving…';
        try {
            const saved = id
                ? await api.request('/api/grades/' + encodeURIComponent(id), { method: 'PUT', body: body })
                : await api.request('/api/grades', { method: 'POST', body: body });
            modal.hide();
            api.toast('success',
                (id ? 'Grade updated. ' : 'Grade added. ') + saved.studentNumber + ' · ' + saved.subjectCode +
                ': ' + saved.grade + ' → ' + saved.remarks);
            await loadGrades(saved.gradeId);
        } catch (error) {
            const highlighted = error.status === 400 && showFieldErrors(error.fieldErrors);
            const message = highlighted ? 'Please fix the highlighted fields.' : error.message;
            api.showAlert(els.formAlert, 'danger', message, api.signInLink(error));
        } finally {
            els.saveButton.disabled = false;
            els.saveButton.textContent = 'Save';
        }
    }

    // ---------------------------------------------------------------- delete

    function askDelete(id) {
        const g = gradesById.get(id);
        pendingDeleteId = id;
        const e = api.escapeHtml;
        els.deleteDetail.innerHTML = g
            ? '<div class="fw-semibold">' + e(g.studentNumber) + ' · ' + e(g.studentName) + '</div>' +
            '<div>' + e(g.subjectCode) + ' — ' + e(g.subjectName) + '</div>' +
            '<div class="small text-muted">Grade ' + e(g.grade) + ' · ' + e(g.semester) + ' ' + e(g.schoolYear) + '</div>'
            : 'Record #' + e(id);
        deleteModal.show();
    }

    async function confirmDelete() {
        const id = pendingDeleteId;
        if (id === null) return;
        const g = gradesById.get(id);
        const label = g ? g.studentNumber + ' – ' + g.subjectCode : '#' + id;
        els.deleteConfirm.disabled = true;
        els.deleteConfirm.innerHTML = '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>Deleting…';
        try {
            await api.request('/api/grades/' + encodeURIComponent(id), { method: 'DELETE' });
            deleteModal.hide();
            api.toast('success', 'Grade record deleted: ' + label);
            await loadGrades();
        } catch (error) {
            deleteModal.hide();
            api.toast('danger', 'Delete failed: ' + error.message);
            if (error.status === 404) await loadGrades();
        } finally {
            pendingDeleteId = null;
            els.deleteConfirm.disabled = false;
            els.deleteConfirm.textContent = 'Delete record';
        }
    }

    // ---------------------------------------------------------------- events

    function reloadSoon() {
        clearTimeout(debounceTimer);
        debounceTimer = setTimeout(() => loadGrades(), 350);
    }

    els.addButton.addEventListener('click', () => openForm(null));
    els.form.addEventListener('submit', saveForm);
    els.grade.addEventListener('input', updateRemarkPreview);
    els.subjectCode.addEventListener('input', () => { els.subjectCode.value = els.subjectCode.value.toUpperCase(); });
    els.modalEl.addEventListener('shown.bs.modal', () => (els.gradeId.value ? els.grade : els.studentId).focus());

    els.filterForm.addEventListener('submit', event => { event.preventDefault(); loadGrades(); });
    els.filterSearch.addEventListener('input', reloadSoon);
    els.filterYear.addEventListener('input', reloadSoon);
    els.filterSemester.addEventListener('change', () => loadGrades());
    els.clearButton.addEventListener('click', () => {
        els.filterForm.reset();
        failedOnly = false;
        els.failedTile.classList.remove('active');
        els.failedTile.setAttribute('aria-pressed', 'false');
        loadGrades();
    });

    els.failedTile.addEventListener('click', () => {
        failedOnly = !failedOnly;
        els.failedTile.classList.toggle('active', failedOnly);
        els.failedTile.setAttribute('aria-pressed', String(failedOnly));
        render();
    });

    els.head.addEventListener('click', event => {
        const btn = event.target.closest('.th-btn');
        if (!btn) return;
        const key = btn.dataset.sort;
        sort = sort.key === key
            ? { key: key, dir: sort.dir === 'asc' ? 'desc' : 'asc' }
            : { key: key, dir: key === 'datePosted' || key === 'grade' ? 'desc' : 'asc' };
        render();
    });

    els.body.addEventListener('click', event => {
        const button = event.target.closest('button[data-action]');
        if (!button) return;
        const id = Number(button.dataset.id);
        if (button.dataset.action === 'edit') {
            const grade = gradesById.get(id);
            if (grade) openForm(grade);
        } else if (button.dataset.action === 'delete') {
            askDelete(id);
        }
    });

    els.deleteConfirm.addEventListener('click', confirmDelete);

    // "/" jumps to search (like GitHub), unless you're already typing somewhere.
    document.addEventListener('keydown', event => {
        if (event.key !== '/' || event.ctrlKey || event.metaKey || event.altKey) return;
        const tag = (event.target.tagName || '').toLowerCase();
        if (tag === 'input' || tag === 'textarea' || tag === 'select' || document.querySelector('.modal.show')) return;
        event.preventDefault();
        els.filterSearch.focus();
    });

    updateSortIndicators();
    loadLookups().then(() => loadGrades());
})();
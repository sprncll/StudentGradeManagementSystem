// Faculty dashboard: view, add, edit and delete grade records through the Web API.
(function () {
    'use strict';

    const api = window.GradeApi;
    const els = {
        alert: document.getElementById('alert-area'),
        body: document.getElementById('grades-body'),
        count: document.getElementById('grades-count'),
        filterForm: document.getElementById('filter-form'),
        filterSearch: document.getElementById('filter-search'),
        filterSemester: document.getElementById('filter-semester'),
        filterYear: document.getElementById('filter-year'),
        addButton: document.getElementById('btn-add'),
        modalEl: document.getElementById('grade-modal'),
        modalTitle: document.getElementById('grade-modal-title'),
        form: document.getElementById('grade-form'),
        formAlert: document.getElementById('form-alert'),
        saveButton: document.getElementById('btn-save'),
        gradeId: document.getElementById('grade-id'),
        studentId: document.getElementById('f-studentId'),
        subjectCode: document.getElementById('f-subjectCode'),
        subjectName: document.getElementById('f-subjectName'),
        grade: document.getElementById('f-grade'),
        semester: document.getElementById('f-semester'),
        schoolYear: document.getElementById('f-schoolYear')
    };
    const modal = new bootstrap.Modal(els.modalEl);
    let gradesById = new Map();

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

    async function loadGrades() {
        const params = new URLSearchParams();
        if (els.filterSearch.value.trim()) params.set('search', els.filterSearch.value.trim());
        if (els.filterSemester.value) params.set('semester', els.filterSemester.value);
        if (els.filterYear.value.trim()) params.set('schoolYear', els.filterYear.value.trim());
        const query = params.toString() ? '?' + params.toString() : '';

        setTableMessage('Loading grade records…', 'text-muted');
        try {
            const grades = await api.request('/api/grades' + query);
            renderGrades(grades);
        } catch (error) {
            setTableMessage('Grade records could not be loaded.', 'text-danger');
            els.count.textContent = '';
            api.showAlert(els.alert, 'danger', error.message, api.signInLink(error));
        }
    }

    function setTableMessage(message, cls) {
        els.body.innerHTML = '<tr><td colspan="8" class="text-center py-4 ' + cls + '">' + api.escapeHtml(message) + '</td></tr>';
    }

    function renderGrades(grades) {
        gradesById = new Map(grades.map(g => [g.gradeId, g]));
        if (grades.length === 0) {
            setTableMessage('No grade records found. Use "Add grade" to post one.', 'text-muted');
            els.count.textContent = '';
            return;
        }
        const e = api.escapeHtml;
        els.body.innerHTML = grades.map(g =>
            '<tr>' +
            '<td><div class="fw-semibold">' + e(g.studentNumber) + '</div><div class="small text-muted">' + e(g.studentName) + '</div></td>' +
            '<td><div class="fw-semibold">' + e(g.subjectCode) + '</div><div class="small text-muted">' + e(g.subjectName) + '</div></td>' +
            '<td class="text-center fs-5">' + e(g.grade) + '</td>' +
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
            '</tr>'
        ).join('');
        els.count.textContent = grades.length + ' record(s)';
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
        try {
            const saved = id
                ? await api.request('/api/grades/' + encodeURIComponent(id), { method: 'PUT', body: body })
                : await api.request('/api/grades', { method: 'POST', body: body });
            modal.hide();
            api.showAlert(els.alert, 'success',
                (id ? 'Grade updated. ' : 'Grade added. ') + saved.studentNumber + ' · ' + saved.subjectCode +
                ': ' + saved.grade + ' → ' + saved.remarks + ' (remark computed by the server).');
            await loadGrades();
        } catch (error) {
            const highlighted = error.status === 400 && showFieldErrors(error.fieldErrors);
            const message = highlighted ? 'Please fix the highlighted fields.' : error.message;
            api.showAlert(els.formAlert, 'danger', message, api.signInLink(error));
        } finally {
            els.saveButton.disabled = false;
        }
    }

    async function deleteGrade(id) {
        const g = gradesById.get(id);
        const label = g ? g.studentNumber + ' ' + g.studentName + ' – ' + g.subjectCode + ' (' + g.grade + ')' : '#' + id;
        if (!window.confirm('Delete this grade record?\n\n' + label + '\n\nThis cannot be undone.')) return;

        try {
            await api.request('/api/grades/' + encodeURIComponent(id), { method: 'DELETE' });
            api.showAlert(els.alert, 'success', 'Grade record deleted: ' + label);
            await loadGrades();
        } catch (error) {
            api.showAlert(els.alert, 'danger', 'Delete failed: ' + error.message, api.signInLink(error));
            if (error.status === 404) await loadGrades();
        }
    }

    // ---------------------------------------------------------------- events

    els.addButton.addEventListener('click', () => openForm(null));
    els.form.addEventListener('submit', saveForm);
    els.filterForm.addEventListener('submit', event => { event.preventDefault(); loadGrades(); });
    els.body.addEventListener('click', event => {
        const button = event.target.closest('button[data-action]');
        if (!button) return;
        const id = Number(button.dataset.id);
        if (button.dataset.action === 'edit') {
            const grade = gradesById.get(id);
            if (grade) openForm(grade);
        } else if (button.dataset.action === 'delete') {
            deleteGrade(id);
        }
    });

    loadLookups().then(loadGrades);
})();

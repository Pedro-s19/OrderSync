document.querySelectorAll('[data-table-search]').forEach(input => {
  input.addEventListener('input', () => {
    const q = input.value.toLocaleLowerCase('es');
    const rows = input.closest('.panel').querySelectorAll('tbody tr');
    rows.forEach(row => row.hidden = !row.innerText.toLocaleLowerCase('es').includes(q));
  });
});

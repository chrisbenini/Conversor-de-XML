<p align="center">
  <img width="100%" src="https://capsule-render.vercel.app/api?type=waving&height=220&color=0:020617,18:0f172a,42:1e3a8a,72:0891b2,100:14b8a6&text=Oficina%20SQL%20Database&fontSize=38&fontColor=f8fafc&fontAlignY=38&desc=Relational%20Modeling%20%7C%20MySQL%20%7C%20Academic%20Project&descAlignY=60&descSize=18&descColor=e0f2fe&animation=twinkling" />
</p>

<p align="center">
  Projeto de <b>modelagem e implementação de banco de dados relacional</b> para uma <b>oficina mecânica</b>, desenvolvido em <b>MySQL</b> com foco em clientes, veículos, equipes, mecânicos, ordens de serviço, peças, serviços e pagamentos.
</p>

<p align="center">
  <img alt="MySQL" src="https://img.shields.io/badge/MySQL-Database-1E3A8A?style=for-the-badge&logo=mysql&logoColor=white">
  <img alt="SQL" src="https://img.shields.io/badge/SQL-Queries-0891B2?style=for-the-badge&logo=postgresql&logoColor=white">
  <img alt="MySQL Workbench" src="https://img.shields.io/badge/MySQL%20Workbench-Data%20Modeling-14B8A6?style=for-the-badge&logo=mysql&logoColor=white">
  <img alt="Academic Project" src="https://img.shields.io/badge/Academic-Project-0F172A?style=for-the-badge">
</p>

---

## `> overview`

Este projeto foi criado para simular o funcionamento de um sistema de **gestão de oficina mecânica**, aplicando conceitos de **modelagem relacional**, **integridade referencial** e **consultas SQL**.

Mesmo sendo um projeto acadêmico da DIO, ele foi desenvolvido com uma organização mais próxima de um cenário real, servindo também como material de estudo e portfólio.

---

## `> entities`

- `cliente`
- `veiculo`
- `mecanico`
- `equipe`
- `equipe_mecanico`
- `ordem_servico`
- `servico`
- `ordem_servico_servico`
- `peca`
- `ordem_servico_peca`
- `pagamento`

---

## `> entity_relationship_diagram`

<p align="center">
  <img src="images/Diagrama.png" width="950" alt="Diagrama relacional do banco de dados da oficina">
</p>

---

## `> physical_structure`

<p align="center">
  <img src="images/Tabelas.png" width="320" alt="Estrutura das tabelas no MySQL Workbench">
</p>

---

## `> project_structure`

```text
Oficina-sql-database
│
├── Images/
│   ├── Diagrama.png
│   └── Tabelas.png
│
├── Sql/
│   ├── 01_create_database.sql
│   ├── 02_create_tables.sql
│   ├── 03_insert_dados.sql
│   └── 04_queries.sql
│
└── README.md
```

---

## `> how_to_run`

```sql
SOURCE Sql/01_create_database.sql;
SOURCE Sql/02_create_tables.sql;
SOURCE Sql/03_insert_dados.sql;
SOURCE Sql/04_queries.sql;
```

---

## `> concepts_applied`

- modelagem relacional
- normalização
- chaves primárias e estrangeiras
- relacionamentos 1:N e N:N
- tabelas associativas
- `JOIN`
- `GROUP BY`
- `ORDER BY`

---

## `> author`

**Christopher Benini**

<p>
  <a href="https://github.com/chrisbenini">
    <img src="https://img.shields.io/badge/GitHub-chrisbenini-0F172A?style=for-the-badge&logo=github&logoColor=white">
  </a>
</p>

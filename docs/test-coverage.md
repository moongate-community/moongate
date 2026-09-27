# Test coverage

How much of the product code the test suite runs. The numbers come from the full
suite (`scripts/test.sh all`, including the PostgreSQL and Redis tests) on the commit
this site was built from. Test projects, protobuf contracts, and source-generated
command-line code are left out.

<!-- coverage-summary -->

## Where the numbers come from

Every CI run executes the tests through `scripts/coverage.sh`, which collects coverage
with coverlet and merges it with ReportGenerator. The run page shows the per-assembly
table, and the HTML report is kept as the `coverage-report` artifact: 90 days for
`main`, 14 days for other branches. Publishing the documentation downloads that
artifact for the published commit.

To produce the same report locally:

```sh
scripts/coverage.sh all
```

Then open `artifacts/coverage/index.html`. The script accepts the same arguments as
`scripts/test.sh`; see [Contribute to Moongate](../CONTRIBUTING.md) for the test
environment.

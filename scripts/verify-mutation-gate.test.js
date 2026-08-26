// Copyright © Erickson Lopez. MIT License.
const assert = require('assert');
const {
  loadThresholds,
  parseScoreFromDescription,
  evaluateScore,
  verifyMutationGate,
  MAX_REPORT_AGE_DAYS
} = require('./verify-mutation-gate');

async function runTests() {
  console.log('Running comprehensive unit tests for verify-mutation-gate.js...\n');

  // Test 1: loadThresholds from stryker-config.json
  {
    const thresholds = loadThresholds();
    assert.strictEqual(thresholds.high, 100, 'Threshold high should be 100');
    assert.strictEqual(thresholds.low, 98, 'Threshold low should be 98');
    assert.strictEqual(thresholds.break, 95, 'Threshold break should be 95');
    console.log('✅ Test 1 Passed: loadThresholds loads correct values from stryker-config.json');
  }

  // Test 2: parseScoreFromDescription
  {
    assert.strictEqual(parseScoreFromDescription('Score: 100% (12/12 packages >= 95%) - ✅ HIGH'), 100);
    assert.strictEqual(parseScoreFromDescription('Score: 98.5% (12/12 packages >= 95%) - 🟡 LOW'), 98.5);
    assert.strictEqual(parseScoreFromDescription('Score: 95.0% - 🟠 WARNING'), 95.0);
    assert.strictEqual(parseScoreFromDescription('Score: 94.2% - ❌ FAILED'), 94.2);
    assert.strictEqual(parseScoreFromDescription(null), null);
    assert.strictEqual(parseScoreFromDescription('No percentage here'), null);
    console.log('✅ Test 2 Passed: parseScoreFromDescription correctly extracts numeric percentage');
  }

  // Test 3: evaluateScore across all boundary bands
  {
    const thresholds = { high: 100, low: 98, break: 95 };

    // >= 100% -> HIGH (Pass)
    const resHigh = evaluateScore(100, thresholds);
    assert.strictEqual(resHigh.status, '✅ HIGH');
    assert.strictEqual(resHigh.passedBreak, true);

    // >= 98% && < 100% -> LOW (Pass)
    const resLow = evaluateScore(98.5, thresholds);
    assert.strictEqual(resLow.status, '🟡 LOW');
    assert.strictEqual(resLow.passedBreak, true);

    // >= 95% && < 98% -> WARNING (Pass, NOT a hard gate)
    const resWarn = evaluateScore(96.0, thresholds);
    assert.strictEqual(resWarn.status, '🟠 WARNING');
    assert.strictEqual(resWarn.passedBreak, true);

    // Exact 95.0% break boundary -> WARNING (Pass)
    const resBreakExact = evaluateScore(95.0, thresholds);
    assert.strictEqual(resBreakExact.status, '🟠 WARNING');
    assert.strictEqual(resBreakExact.passedBreak, true);

    // < 95% -> FAILED (Fail, hard gate)
    const resFail = evaluateScore(94.9, thresholds);
    assert.strictEqual(resFail.status, '❌ FAILED');
    assert.strictEqual(resFail.passedBreak, false);

    console.log('✅ Test 3 Passed: evaluateScore correctly categorizes scores and break gate');
  }

  // Test 4: verifyMutationGate with direct target commit at 100%
  {
    let failed = false;
    const mockContext = {
      repo: { owner: 'ericksonlopezf', repo: 'dotnet-auditing' },
      sha: 'target1234567890'
    };

    const freshDate = new Date().toISOString();

    const mockGithub = {
      rest: {
        repos: {
          getCombinedStatusForRef: async ({ ref }) => {
            if (ref === 'target1234567890') {
              return {
                data: {
                  statuses: [
                    {
                      context: 'mutation-testing/stryker',
                      state: 'success',
                      description: 'Score: 100.00% (12/12 packages >= 95%) - ✅ HIGH',
                      updated_at: freshDate,
                      target_url: 'https://github.com/ericksonlopezf/dotnet-auditing/actions/runs/1001'
                    }
                  ]
                }
              };
            }
            return { data: { statuses: [] } };
          }
        }
      }
    };

    const mockCore = {
      setFailed: () => { failed = true; },
      summary: { addRaw: () => ({ write: async () => {} }) },
      setOutput: () => {}
    };

    await verifyMutationGate({ github: mockGithub, context: mockContext, core: mockCore });
    assert.strictEqual(failed, false, 'Should pass for 100% score on target commit');
    console.log('✅ Test 4 Passed: verifyMutationGate succeeds with direct 100% commit status');
  }

  // Test 5: verifyMutationGate with score below break threshold (< 95%)
  {
    let failed = false;
    const mockContext = {
      repo: { owner: 'ericksonlopezf', repo: 'dotnet-auditing' },
      sha: 'fail1234567890'
    };

    const freshDate = new Date().toISOString();

    const mockGithub = {
      rest: {
        repos: {
          getCombinedStatusForRef: async () => {
            return {
              data: {
                statuses: [
                  {
                    context: 'mutation-testing/stryker',
                    state: 'failure',
                    description: 'Score: 88.50% (10/12 packages >= 95%) - ❌ FAILED',
                    updated_at: freshDate,
                    target_url: 'https://github.com/ericksonlopezf/dotnet-auditing/actions/runs/1002'
                  }
                ]
              }
            };
          }
        }
      }
    };

    const mockCore = {
      setFailed: () => { failed = true; },
      summary: { addRaw: () => ({ write: async () => {} }) },
      setOutput: () => {}
    };

    try {
      await verifyMutationGate({ github: mockGithub, context: mockContext, core: mockCore });
      assert.fail('Should have thrown an error for score below break threshold');
    } catch (err) {
      assert.strictEqual(failed, true, 'core.setFailed should be called');
      console.log('✅ Test 5 Passed: verifyMutationGate blocks release for sub-break score (88.5%)');
    }
  }

  // Test 6: verifyMutationGate with WARNING score (96.5% >= 95% break) -> Release permitted
  {
    let failed = false;
    const mockContext = {
      repo: { owner: 'ericksonlopezf', repo: 'dotnet-auditing' },
      sha: 'warn1234567890'
    };

    const freshDate = new Date().toISOString();

    const mockGithub = {
      rest: {
        repos: {
          getCombinedStatusForRef: async () => {
            return {
              data: {
                statuses: [
                  {
                    context: 'mutation-testing/stryker',
                    state: 'success',
                    description: 'Score: 96.50% (12/12 packages >= 95%) - 🟠 WARNING',
                    updated_at: freshDate,
                    target_url: 'https://github.com/ericksonlopezf/dotnet-auditing/actions/runs/1003'
                  }
                ]
              }
            };
          }
        }
      }
    };

    const mockCore = {
      setFailed: () => { failed = true; },
      summary: { addRaw: () => ({ write: async () => {} }) },
      setOutput: () => {}
    };

    await verifyMutationGate({ github: mockGithub, context: mockContext, core: mockCore });
    assert.strictEqual(failed, false, 'Score >= 95% should allow release (WARNING is not a hard block)');
    console.log('✅ Test 6 Passed: verifyMutationGate permits release for 96.5% WARNING score (>= 95% break)');
  }

  // Test 7: verifyMutationGate searching ancestor commit on main with zero src/ drift
  {
    let failed = false;
    const mockContext = {
      repo: { owner: 'ericksonlopezf', repo: 'dotnet-auditing' },
      sha: 'newReleaseCommit777'
    };

    const freshDate = new Date().toISOString();

    const mockGithub = {
      rest: {
        repos: {
          getCombinedStatusForRef: async ({ ref }) => {
            if (ref === 'ancestorCommit555') {
              return {
                data: {
                  statuses: [
                    {
                      context: 'mutation-testing/stryker',
                      state: 'success',
                      description: 'Score: 99.10% (12/12 packages >= 95%) - 🟡 LOW',
                      updated_at: freshDate,
                      target_url: 'https://github.com/ericksonlopezf/dotnet-auditing/actions/runs/1004'
                    }
                  ]
                }
              };
            }
            return { data: { statuses: [] } };
          },
          listCommits: async () => {
            return {
              data: [
                { sha: 'newReleaseCommit777' },
                { sha: 'ancestorCommit555' }
              ]
            };
          },
          compareCommits: async () => {
            return {
              data: {
                files: [
                  { filename: 'README.md' },
                  { filename: 'CHANGELOG.md' }
                ]
              }
            };
          }
        }
      }
    };

    const mockCore = {
      setFailed: () => { failed = true; },
      summary: { addRaw: () => ({ write: async () => {} }) },
      setOutput: () => {}
    };

    await verifyMutationGate({ github: mockGithub, context: mockContext, core: mockCore });
    assert.strictEqual(failed, false, 'Should allow release using ancestor commit when only docs changed');
    console.log('✅ Test 7 Passed: verifyMutationGate allows ancestor commit when zero src/ drift detected');
  }

  // Test 8: verifyMutationGate blocks release if src/ drift detected
  {
    let failed = false;
    const mockContext = {
      repo: { owner: 'ericksonlopezf', repo: 'dotnet-auditing' },
      sha: 'driftedReleaseCommit888'
    };

    const freshDate = new Date().toISOString();

    const mockGithub = {
      rest: {
        repos: {
          getCombinedStatusForRef: async ({ ref }) => {
            if (ref === 'ancestorCommit555') {
              return {
                data: {
                  statuses: [
                    {
                      context: 'mutation-testing/stryker',
                      state: 'success',
                      description: 'Score: 100.00% (12/12 packages >= 95%) - ✅ HIGH',
                      updated_at: freshDate,
                      target_url: 'https://github.com/ericksonlopezf/dotnet-auditing/actions/runs/1005'
                    }
                  ]
                }
              };
            }
            return { data: { statuses: [] } };
          },
          listCommits: async () => {
            return {
              data: [
                { sha: 'driftedReleaseCommit888' },
                { sha: 'ancestorCommit555' }
              ]
            };
          },
          compareCommits: async () => {
            return {
              data: {
                files: [
                  { filename: 'src/EricksonLopez.Auditing/AuditLogger.cs' }
                ]
              }
            };
          }
        }
      }
    };

    const mockCore = {
      setFailed: () => { failed = true; },
      summary: { addRaw: () => ({ write: async () => {} }) },
      setOutput: () => {}
    };

    try {
      await verifyMutationGate({ github: mockGithub, context: mockContext, core: mockCore });
      assert.fail('Should have thrown an error due to src/ drift');
    } catch (err) {
      assert.strictEqual(failed, true, 'core.setFailed should be called when src/ code drift is detected');
      console.log('✅ Test 8 Passed: verifyMutationGate blocks release when unanalyzed src/ code changes exist');
    }
  }

  // Test 9: verifyMutationGate blocks release if mutation report is expired (> 7 days)
  {
    let failed = false;
    const mockContext = {
      repo: { owner: 'ericksonlopezf', repo: 'dotnet-auditing' },
      sha: 'staleCommit999'
    };

    const oldDate = new Date(Date.now() - (MAX_REPORT_AGE_DAYS + 2) * 24 * 60 * 60 * 1000).toISOString();

    const mockGithub = {
      rest: {
        repos: {
          getCombinedStatusForRef: async () => {
            return {
              data: {
                statuses: [
                  {
                    context: 'mutation-testing/stryker',
                    state: 'success',
                    description: 'Score: 100.00% (12/12 packages >= 95%) - ✅ HIGH',
                    updated_at: oldDate,
                    target_url: 'https://github.com/ericksonlopezf/dotnet-auditing/actions/runs/1006'
                  }
                ]
              }
            };
          }
        }
      }
    };

    const mockCore = {
      setFailed: () => { failed = true; },
      summary: { addRaw: () => ({ write: async () => {} }) },
      setOutput: () => {}
    };

    try {
      await verifyMutationGate({ github: mockGithub, context: mockContext, core: mockCore });
      assert.fail('Should have thrown an error due to expired report');
    } catch (err) {
      assert.strictEqual(failed, true, 'core.setFailed should be called when report is older than TTL');
      console.log(`✅ Test 9 Passed: verifyMutationGate blocks release when report is expired (> ${MAX_REPORT_AGE_DAYS} days)`);
    }
  }

  console.log('\n🎉 ALL 9 VERIFICATION TESTS PASSED SUCCESSFULLY!\n');
}

runTests().catch(err => {
  console.error('Test run failed:', err);
  process.exit(1);
});

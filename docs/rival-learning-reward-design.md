# Rival learning reward design

Status: Owner-approved initial balance, connected to the rival result flow.
CoffeeGAME applies it through `PlayerProgression`, displays the actual delta, and
persists the balance, affinity, recruitment, and consumed grant in profile v2.

## Reward authority

A reward is granted only when CoffeeLearning returns one authoritative completed
result with all of the following:

- `judgment.isCorrect = true`
- `learning.mutationApplied = true`
- `rewardEligibility.eligible = true`
- a nonblank stable `grantId`

`grantId` is the exactly-once key. Replaying the same result grants nothing. A
pending, incorrect, failed, or unapplied result grants nothing. An incorrect
answer does not reduce money or affinity; it remains a later weak-question
candidate instead.

## Approved initial values

The provider supplies only semantic difficulty. CoffeeGAME owns the amounts.

| Band | Level | Talent | EXP | Gold | Rival affinity |
| --- | ---: | ---: | ---: | ---: | ---: |
| Foundation | 1 | 1 | 2 | 1 | 3 |
| Foundation | 2 | 1 | 4 | 2 | 4 |
| Intermediate | 3 | 2 | 9 | 6 | 6 |
| Intermediate | 4 | 2 | 12 | 8 | 7 |
| Advanced | 5 | 3 | 20 | 15 | 9 |

Gold is the existing game-local Gold balance used by combat rewards; it is not a
CoffeeLearning currency or server-selected amount. Slimes currently give Gold 1,
so the table values one successful rival answer at roughly one through fifteen
slime rewards according to difficulty.

Affinity is stored per stable rival ID. The recruitment threshold is
100. Crossing 100 recruits that rival exactly once; affinity can continue to be
shown after recruitment but does not recruit repeatedly. With the initial table,
recruitment takes roughly 12 to 34 correct encounters depending on difficulty.

## Persistence and presentation

The profile must migrate from v1 to v2 and atomically persist:

- existing Gold and EXP through `PlayerProgression`;
- a new spendable talent-points balance, separate from the selected `talentId`;
- affinity and recruited state by rival ID;
- consumed learning `grantId` values.

The commit order is compute all bounded next values, consume the grant ID as the
commit gate, apply every balance/relationship value, then save once. A crash or
result replay must never split Gold from affinity or award twice.

The completed encounter should show the actual delta and total, for example:

```text
正解！ Gold +6 / EXP +9 / 才能 +2
白銀のライバル 親密度 +6（42 / 100）
```

On threshold crossing it additionally shows `仲間になった`. The inventory menu
shows Gold and spendable talent points; the companions menu shows the silver
rival's current affinity and recruited state.

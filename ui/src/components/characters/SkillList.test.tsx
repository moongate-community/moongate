import { render, screen } from '@testing-library/react'

import '../../lib/i18n'
import { SkillList } from './SkillList'
import type { CharacterSkill } from '../../lib/characters'

function skill(name: string, over: Partial<CharacterSkill> = {}): CharacterSkill {
  return { id: 1, name, value: 50, cap: 100, lock: 'Up', ...over } as CharacterSkill
}

describe('SkillList', () => {
  it('shows each skill with its value and cap', () => {
    render(<SkillList skills={[skill('Alchemy', { value: 62.5 })]} />)

    expect(screen.getByText('Alchemy')).toBeInTheDocument()
    expect(screen.getByText('62.5')).toBeInTheDocument()
    expect(screen.getByText('100')).toBeInTheDocument()
  })

  // A value with no decimal reads better as 50 than 50.0, and one with a decimal must keep it: the
  // server reports tenths of a point, so 62.5 is a real value and not a rounding artefact.
  it('keeps the tenth only when there is one', () => {
    render(<SkillList skills={[skill('Alchemy', { id: 1, value: 50 }), skill('Mining', { id: 2, value: 62.5 })]} />)

    expect(screen.getByText('50')).toBeInTheDocument()
    expect(screen.getByText('62.5')).toBeInTheDocument()
  })

  it('names the lock in words', () => {
    render(
      <SkillList
        skills={[
          skill('Alchemy', { id: 1, lock: 'Up' }),
          skill('Mining', { id: 2, lock: 'Down' }),
          skill('Tailoring', { id: 3, lock: 'Locked' }),
        ]}
      />,
    )

    expect(screen.getByText('Rising')).toBeInTheDocument()
    expect(screen.getByText('Falling')).toBeInTheDocument()
    expect(screen.getByText('Locked')).toBeInTheDocument()
  })

  // The registry can lag the world, so the server may report a lock it has no word for. Showing the
  // server's own value beats showing a raw translation key.
  it('shows a lock it has no word for rather than a key', () => {
    render(<SkillList skills={[skill('Alchemy', { lock: 'Sideways' })]} />)

    expect(screen.getByText('Sideways')).toBeInTheDocument()
  })

  it('reports the total of every skill', () => {
    render(<SkillList skills={[skill('Alchemy', { id: 1, value: 50 }), skill('Mining', { id: 2, value: 32.5 })]} />)

    expect(screen.getByText('82.5 total')).toBeInTheDocument()
  })

  // A brand-new character can have none, and an empty frame says nothing.
  it('says so when the character has trained nothing', () => {
    render(<SkillList skills={[]} />)

    expect(screen.getByText('No skills trained')).toBeInTheDocument()
  })
})

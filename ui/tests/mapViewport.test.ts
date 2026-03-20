import assert from 'node:assert/strict'
import test from 'node:test'

import { getContainedSize, getFitScale, getMapTransformStyles } from '../src/pages/mapViewport.ts'

test('getMapTransformStyles preserves native map dimensions for the transform content', () => {
  const styles = getMapTransformStyles(7168, 4096)

  assert.equal(styles.wrapperStyle.width, '100%')
  assert.equal(styles.wrapperStyle.height, '100%')
  assert.equal(styles.contentStyle.width, '7168px')
  assert.equal(styles.contentStyle.height, '4096px')
})

test('getFitScale preserves aspect ratio by using the smaller viewport ratio', () => {
  const scale = getFitScale({
    viewportWidth: 1200,
    viewportHeight: 800,
    contentWidth: 7168,
    contentHeight: 4096,
  })

  assert.equal(scale, 1200 / 7168)
})

test('getContainedSize fits the map into the viewport without stretching it', () => {
  const size = getContainedSize({
    viewportWidth: 1200,
    viewportHeight: 800,
    contentWidth: 7168,
    contentHeight: 4096,
  })

  assert.deepEqual(size, {
    width: 1200,
    height: Math.round(4096 * (1200 / 7168)),
  })
})

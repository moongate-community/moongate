export interface MapTransformStyles {
  wrapperStyle: {
    width: string
    height: string
    cursor: 'grab'
  }
  contentStyle: {
    width: string
    height: string
  }
}

export function getMapTransformStyles(mapWidth: number, mapHeight: number): MapTransformStyles {
  return {
    wrapperStyle: {
      width: '100%',
      height: '100%',
      cursor: 'grab',
    },
    contentStyle: {
      width: `${mapWidth}px`,
      height: `${mapHeight}px`,
    },
  }
}

export interface FitScaleInput {
  viewportWidth: number
  viewportHeight: number
  contentWidth: number
  contentHeight: number
}

export interface ContainedSizeInput extends FitScaleInput {}

export interface ContainedSize {
  width: number
  height: number
}

export function getFitScale({
  viewportWidth,
  viewportHeight,
  contentWidth,
  contentHeight,
}: FitScaleInput): number {
  if (viewportWidth <= 0 || viewportHeight <= 0 || contentWidth <= 0 || contentHeight <= 0) {
    return 1
  }

  return Math.min(viewportWidth / contentWidth, viewportHeight / contentHeight)
}

export function getContainedSize({
  viewportWidth,
  viewportHeight,
  contentWidth,
  contentHeight,
}: ContainedSizeInput): ContainedSize {
  const scale = getFitScale({
    viewportWidth,
    viewportHeight,
    contentWidth,
    contentHeight,
  })

  if (scale <= 0 || !Number.isFinite(scale)) {
    return {
      width: contentWidth,
      height: contentHeight,
    }
  }

  return {
    width: Math.max(1, Math.round(contentWidth * scale)),
    height: Math.max(1, Math.round(contentHeight * scale)),
  }
}

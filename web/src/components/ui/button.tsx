import * as React from "react"
import { cva, type VariantProps } from "class-variance-authority"
import { cn } from "@/lib/utils"
import { Slot } from "radix-ui"

// Tuesday actions: one black primary per page or dialog, outlined secondaries with a 3:1 boundary, explicit destructive
// wording in red, blue underlined links. Disabled stays readable. Heights follow the density tokens (40 px standalone,
// 32 px inside compact rows, 44 px comfortable or touch).
const buttonVariants = cva(
  "inline-flex shrink-0 items-center justify-center gap-2 rounded-md text-sm font-medium whitespace-nowrap transition-colors duration-[120ms] outline-none focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring disabled:pointer-events-none aria-invalid:border-destructive [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4",
  {
    variants: {
      variant: {
        default: "bg-action text-action-foreground hover:bg-action-hover disabled:bg-disabled disabled:text-disabled-foreground",
        destructive: "bg-destructive text-white hover:bg-destructive/90 disabled:bg-disabled disabled:text-disabled-foreground",
        outline: "border border-input bg-card text-foreground hover:bg-muted disabled:border-border disabled:bg-disabled disabled:text-disabled-foreground",
        secondary: "bg-secondary text-secondary-foreground hover:bg-secondary/80 disabled:text-disabled-foreground",
        ghost: "hover:bg-muted hover:text-foreground disabled:text-disabled-foreground",
        link: "text-primary underline underline-offset-4 hover:text-foreground disabled:text-disabled-foreground",
      },
      size: {
        default: "min-h-(--control-h) px-4 py-2 has-[>svg]:px-3",
        xs: "min-h-(--control-row-h) gap-1 px-2 text-xs has-[>svg]:px-1.5 [&_svg:not([class*='size-'])]:size-3.5",
        sm: "min-h-(--control-row-h) gap-1.5 px-3 has-[>svg]:px-2.5",
        lg: "min-h-11 px-6 has-[>svg]:px-4",
        icon: "size-(--control-h)",
        "icon-xs": "size-(--control-row-h) [&_svg:not([class*='size-'])]:size-3.5",
        "icon-sm": "size-(--control-row-h)",
        "icon-lg": "size-11",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  }
)

function Button({
  className,
  variant = "default",
  size = "default",
  asChild = false,
  ...props
}: React.ComponentProps<"button"> &
  VariantProps<typeof buttonVariants> & {
    asChild?: boolean
  }) {
  const Comp = asChild ? Slot.Root : "button"

  return (
    <Comp
      data-slot="button"
      data-variant={variant}
      data-size={size}
      className={cn(buttonVariants({ variant, size, className }))}
      {...props}
    />
  )
}

export { Button, buttonVariants }
